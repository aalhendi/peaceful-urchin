using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Access.Api.Tests;

public sealed class AccessFlowTests
{
    [Fact]
    public async Task DemoUsersLoginWithTheirInitialRoles()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.Client, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.Client, "cinet@example.test", "cinet-local");
        var bank = await ActorAsync(app.Client, bankToken);
        var cinet = await ActorAsync(app.Client, cinetToken);

        Assert.NotEqual(bank.InstitutionId, cinet.InstitutionId);
        Assert.Equal("Bank", bank.InstitutionKind);
        Assert.Equal("CINET", cinet.InstitutionKind);
        Assert.Equal(["CustomerReader", "LoanCreator", "PaymentWriter"], bank.Roles);
        Assert.Equal(["Customer.Read", "Loan.Create", "Payment.Write"], bank.Permissions);
        Assert.Equal(["AccessAdmin", "CreditAnalyst", "CustomerReader", "LoanBlocker"], cinet.Roles);
        Assert.Equal(["StaffRoles.Change", "Credit.Read", "Customer.Read", "Loan.Block"], cinet.Permissions);
    }

    [Fact]
    public async Task CinetRoleChangeAffectsExistingTokenAndSurvivesRestart()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.Client, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.Client, "cinet@example.test", "cinet-local");
        var bankBefore = await ActorAsync(app.Client, bankToken);

        using var change = await ReplaceRolesAsync(app.Client, cinetToken, bankBefore.ActorId,
            ["CustomerWriter"]);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var bankAfter = await ActorAsync(app.Client, bankToken);
        Assert.Equal(bankBefore.ActorId, bankAfter.ActorId);
        Assert.Equal(["CustomerWriter"], bankAfter.Roles);
        Assert.Equal(["Customer.Write"], bankAfter.Permissions);

        await app.RestartAsync();
        var bankAfterRestart = await ActorAsync(app.Client, bankToken);
        Assert.Equal(bankAfter.ActorId, bankAfterRestart.ActorId);
        Assert.Equal(bankAfter.InstitutionId, bankAfterRestart.InstitutionId);
        Assert.Equal(bankAfter.Roles, bankAfterRestart.Roles);
        Assert.Equal(bankAfter.Permissions, bankAfterRestart.Permissions);
    }

    [Fact]
    public async Task BankUserCannotChangeRoles()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.Client, "bank@example.test", "bank-local");
        var bankBefore = await ActorAsync(app.Client, bankToken);

        using var attempt = await ReplaceRolesAsync(app.Client, bankToken, bankBefore.ActorId,
            ["CustomerWriter"]);
        Assert.Equal(HttpStatusCode.Forbidden, attempt.StatusCode);

        var bankAfter = await ActorAsync(app.Client, bankToken);
        Assert.Equal(bankBefore.ActorId, bankAfter.ActorId);
        Assert.Equal(bankBefore.Roles, bankAfter.Roles);
        Assert.Equal(bankBefore.Permissions, bankAfter.Permissions);
    }

    [Fact]
    public async Task InvalidCredentialsReturnUnauthorized()
    {
        await using var app = await TestApp.StartAsync();

        using var unknown = await app.Client.PostAsJsonAsync("/auth/login",
            new { UserName = "missing@example.test", Password = "wrong" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        using var wrongPassword = await app.Client.PostAsJsonAsync("/auth/login",
            new { UserName = "bank@example.test", Password = "wrong" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoutesRequireAValidBearerSession()
    {
        await using var app = await TestApp.StartAsync();

        using var actor = await app.Client.GetAsync("/auth/actor", TestContext.Current.CancellationToken);
        using var logout = await app.Client.PostAsync("/auth/logout", null, TestContext.Current.CancellationToken);
        using var change = await app.Client.PutAsJsonAsync($"/staff-users/{Guid.NewGuid()}/roles",
            new { Roles = new[] { "CustomerReader" } }, TestContext.Current.CancellationToken);
        using var malformedRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/actor");
        malformedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "bad-token");
        using var malformed = await app.Client.SendAsync(malformedRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, actor.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        Assert.Equal("Bearer", actor.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task LoginRateLimitDoesNotThrottleLiveness()
    {
        await using var app = await TestApp.StartAsync();
        var statuses = new HttpStatusCode[11];

        for (var attempt = 0; attempt < statuses.Length; attempt++)
        {
            using var response = await app.Client.PostAsJsonAsync("/auth/login",
                new { UserName = "missing@example.test", Password = "wrong" }, TestContext.Current.CancellationToken);
            statuses[attempt] = response.StatusCode;
        }

        using var live = await app.Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.All(statuses[..10], status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task InvalidRoleAssignmentsDoNotChangeRoles()
    {
        await using var app = await TestApp.StartAsync();
        var cinetToken = await LoginAsync(app.Client, "cinet@example.test", "cinet-local");
        var cinetBefore = await ActorAsync(app.Client, cinetToken);
        var bankToken = await LoginAsync(app.Client, "bank@example.test", "bank-local");
        var bankBefore = await ActorAsync(app.Client, bankToken);

        using var incompatible = await ReplaceRolesAsync(app.Client, cinetToken, cinetBefore.ActorId,
            ["LoanCreator"]);
        Assert.Equal(HttpStatusCode.BadRequest, incompatible.StatusCode);

        using var wrongInstitution = await ReplaceRolesAsync(app.Client, cinetToken, bankBefore.ActorId,
            ["LoanBlocker"]);
        Assert.Equal(HttpStatusCode.BadRequest, wrongInstitution.StatusCode);

        using var unknown = await ReplaceRolesAsync(app.Client, cinetToken, cinetBefore.ActorId,
            ["UnknownRole"]);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var cinetAfter = await ActorAsync(app.Client, cinetToken);
        Assert.Equal(cinetBefore.ActorId, cinetAfter.ActorId);
        Assert.Equal(cinetBefore.Roles, cinetAfter.Roles);
        Assert.Equal(cinetBefore.Permissions, cinetAfter.Permissions);
    }

    [Fact]
    public async Task LogoutRevokesBearerToken()
    {
        await using var app = await TestApp.StartAsync();
        var token = await LoginAsync(app.Client, "bank@example.test", "bank-local");

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var logoutResponse = await app.Client.SendAsync(logout, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var afterLogout = await SendActorAsync(app.Client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task DisablingUserRejectsExistingSessionAndNewLogin()
    {
        await using var app = await TestApp.StartAsync();
        var token = await LoginAsync(app.Client, "bank@example.test", "bank-local");
        await app.ExecuteAsync("""
                               UPDATE staff_users SET active = false WHERE username = 'bank@example.test'
                               """);

        using var existingSession = await SendActorAsync(app.Client, token);
        using var newLogin = await app.Client.PostAsJsonAsync("/auth/login",
            new { UserName = "bank@example.test", Password = "bank-local" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, existingSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, newLogin.StatusCode);
    }

    [Fact]
    public async Task ExpiredBearerTokenIsRejected()
    {
        await using var app = await TestApp.StartAsync();
        var token = await LoginAsync(app.Client, "cinet@example.test", "cinet-local");
        await app.ExecuteAsync("""
                               UPDATE staff_sessions SET expires_at = now() - interval '1 minute'
                               WHERE staff_user_id = (SELECT id FROM staff_users WHERE username = 'cinet@example.test')
                               """);

        using var afterExpiry = await SendActorAsync(app.Client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterExpiry.StatusCode);
    }

    [Fact]
    public async Task InvalidStoredRoleDoesNotBecomeAnActorPermission()
    {
        await using var app = await TestApp.StartAsync();
        var token = await LoginAsync(app.Client, "cinet@example.test", "cinet-local");
        await app.ExecuteAsync("""
                               INSERT INTO staff_user_roles (staff_user_id, role)
                               SELECT id, 'UnknownRole' FROM staff_users WHERE username = 'cinet@example.test'
                               """);

        using var response = await SendActorAsync(app.Client, token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var liveRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        liveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var live = await app.Client.SendAsync(liveRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsBearerSecurity()
    {
        await using var app = await TestApp.StartAsync();

        using var response = await app.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document =
            await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken);
        Assert.NotNull(document);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/staff-users/{userId}/roles").GetProperty("put")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/auth/actor").GetProperty("get")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/auth/logout").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.False(paths.GetProperty("/auth/login").GetProperty("post")
            .TryGetProperty("security", out _));
    }

    [Fact]
    public async Task LivenessResponds()
    {
        await using var app = await TestApp.StartAsync();

        using var response = await app.Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alive", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/login",
            new { UserName = username, Password = password }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(login);
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(43, login.AccessToken.Length);
        return login.AccessToken;
    }

    private static async Task<ActorResponse> ActorAsync(HttpClient client, string token)
    {
        using var response = await SendActorAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<ActorResponse>(TestContext.Current.CancellationToken)
               ?? throw new InvalidOperationException("Actor response was empty.");
    }

    private static async Task<HttpResponseMessage> SendActorAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/actor");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> ReplaceRolesAsync(
        HttpClient client, string token, Guid userId, string[] roles)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/staff-users/{userId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { Roles = roles });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record LoginResponse(string TokenType, string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record ActorResponse(
        Guid ActorId,
        Guid InstitutionId,
        string InstitutionKind,
        string[] Roles,
        string[] Permissions);

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;
        private readonly string _connectionString;
        private WebApplicationFactory<Program> _factory;

        private TestApp(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            _connectionString = connectionString;
            _factory = new AccessFactory(connectionString);
            Client = _factory.CreateClient();
        }

        public HttpClient Client { get; private set; }

        public static async Task<TestApp> StartAsync()
        {
            var adminConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
                                        ??
                                        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=local-demo";
            var databaseName = $"test_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync(TestContext.Current.CancellationToken);
                // NOTE(aalhendi): PostgreSQL cannot parameterize identifiers; this name contains only a generated GUID.
                await admin.ExecuteAsync($"CREATE DATABASE \"{databaseName}\"");
            }

            var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName
            }.ConnectionString;
            return new TestApp(adminConnectionString, databaseName, connectionString);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(sql);
        }

        public async Task RestartAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            _factory = new AccessFactory(_connectionString);
            Client = _factory.CreateClient();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.ExecuteAsync($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)");
        }

        private sealed class AccessFactory(string connectionString) : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Access"] = connectionString
                    }));
            }
        }
    }
}
