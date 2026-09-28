using System.Threading.RateLimiting;
using Access.Api.Application;
using Access.Api.Domain;
using Access.Api.Infrastructure;
using Access.Api.Web;
using Database.Migrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [StaffSessionHttp.Scheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "Opaque session token returned by /auth/login."
            }
        };
        return Task.CompletedTask;
    });
});
builder.Services.AddSingleton(serviceProvider =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Access")
                           ?? throw new InvalidOperationException("ConnectionStrings:Access is required.");
    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<IStaffAccessStore, AccessStore>();
builder.Services.AddScoped<AccessService>();
// TODO(aalhendi): Review password hashing cost and whether a server-side pepper is needed before production use.
builder.Services.AddSingleton<IPasswordHasher<StaffUser>>(new PasswordHasher<StaffUser>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
app.UseRouting();
app.UseRateLimiter();

var connectionString = app.Configuration.GetConnectionString("Access")
                       ?? throw new InvalidOperationException("ConnectionStrings:Access is required.");
foreach (var name in await MigrationRunner.ApplyAsync(connectionString, typeof(Program).Assembly,
             "Access.Api.Migrations."))
    app.Logger.LogInformation("Applied database migration {Migration}", name);

app.MapRoutes();

app.Run();

// NOTE(aalhendi): Makes the top-level Program accessible to WebApplicationFactory<Program> in tests.
public partial class Program;
