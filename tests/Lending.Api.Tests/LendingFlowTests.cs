extern alias AccessApi;
extern alias CreditApi;
extern alias LendingApi;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Lending.Api.Tests;

public sealed class LendingFlowTests
{
    private const string CustomerId = "180010100006";
    private const string SecondCustomerId = "304022900002";
    private const string UnknownCustomerId = "180010100014";

    [Fact]
    public async Task BankCreatesLoanInItsOwnInstitutionAndCinetCanBlockFurtherLoans()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        var bank = await ActorAsync(app.AccessClient, bankToken);
        var cinet = await ActorAsync(app.AccessClient, cinetToken);

        using var created = await CreateLoanAsync(app.LendingClient, bankToken, new
        {
            CustomerCivilId = CustomerId,
            InstitutionId = Guid.NewGuid(),
            StartDate = "2026-10-01",
            TenorMonths = 24,
            AmountKwd = 1000.125m,
            RatePercent = 7.5m,
            Installments = DemoInstallments(24)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = await created.Content.ReadFromJsonAsync<LoanReceipt>(TestContext.Current.CancellationToken);
        Assert.NotNull(receipt);
        var stored = await app.FindLoanAsync(receipt.Id);
        Assert.Equal(CustomerId, stored.CustomerCivilId);
        Assert.Equal(bank.InstitutionId, stored.InstitutionId);
        Assert.Equal(new DateOnly(2026, 10, 1), stored.StartDate);
        Assert.Equal(24, stored.TenorMonths);
        Assert.Equal(1000.125m, stored.AmountKwd);
        Assert.Equal(7.5m, stored.RatePercent);
        Assert.Equal("Open", stored.Status);

        using var initiallyEligible = await CheckEligibilityAsync(app.LendingClient, bankToken);
        Assert.True((await ReadEligibilityAsync(initiallyEligible)).Eligible);

        using var blocked = await SetBlockAsync(app.LendingClient, cinetToken, true);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        using var blockedEligibility = await CheckEligibilityAsync(app.LendingClient, bankToken);
        Assert.False((await ReadEligibilityAsync(blockedEligibility)).Eligible);
        using var sameBlock = await SetBlockAsync(app.LendingClient, cinetToken, true);
        Assert.Equal(HttpStatusCode.NoContent, sameBlock.StatusCode);
        using var refused = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(1, await app.CountLoansAsync());

        using var unblocked = await SetBlockAsync(app.LendingClient, cinetToken, false);
        Assert.Equal(HttpStatusCode.NoContent, unblocked.StatusCode);
        using var restoredEligibility = await CheckEligibilityAsync(app.LendingClient, bankToken);
        Assert.True((await ReadEligibilityAsync(restoredEligibility)).Eligible);
        using var createdAgain = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Created, createdAgain.StatusCode);
        Assert.Equal(2, await app.CountLoansAsync());
        var changes = await app.FindBlockChangesAsync();
        Assert.Equal([true, false], changes.Select(change => change.Blocked));
        Assert.All(changes, change => Assert.Equal(cinet.ActorId, change.ChangedBy));
        Assert.All(changes, change => Assert.True(change.ChangedAt <= DateTime.UtcNow));
    }

    [Fact]
    public async Task LendingChecksCurrentAccessPermissionsAndActorKind()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        var bank = await ActorAsync(app.AccessClient, bankToken);

        using var bankBlock = await SetBlockAsync(app.LendingClient, bankToken, true);
        using var cinetLoan = await CreateLoanAsync(app.LendingClient, cinetToken);
        using var creditBefore = await LookupCustomerAsync(app.CreditClient, bankToken);
        Assert.Equal(HttpStatusCode.Forbidden, bankBlock.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cinetLoan.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, creditBefore.StatusCode);

        using var roleChange = new HttpRequestMessage(HttpMethod.Put, $"/staff-users/{bank.ActorId}/roles");
        roleChange.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cinetToken);
        roleChange.Content = JsonContent.Create(new { Roles = new[] { "CustomerReader" } });
        using var changed = await app.AccessClient.SendAsync(roleChange, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var revoked = await CreateLoanAsync(app.LendingClient, bankToken);
        using var creditAfter = await LookupCustomerAsync(app.CreditClient, bankToken);
        using var eligibilityAfter = await CheckEligibilityAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, creditAfter.StatusCode);
        Assert.True((await ReadEligibilityAsync(eligibilityAfter)).Eligible);
        Assert.Equal(0, await app.CountLoansAsync());
    }

    [Fact]
    public async Task InvalidInputsAndInvalidSessionDoNotWriteLoans()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");

        using var missingToken = await CreateLoanAsync(app.LendingClient, null);
        using var creditWithoutToken = await LookupCustomerAsync(app.CreditClient, null);
        using var malformedToken = await CreateLoanAsync(app.LendingClient, "bad-token");
        using var invalid = await CreateLoanAsync(app.LendingClient, bankToken,
            new
            {
                CustomerCivilId = CustomerId, StartDate = "2026-10-01", TenorMonths = 0,
                AmountKwd = -1m, RatePercent = 7.5m
            });
        using var invalidSchedule = await CreateLoanAsync(app.LendingClient, bankToken, new
        {
            CustomerCivilId = CustomerId,
            StartDate = "2026-10-01",
            TenorMonths = 2,
            AmountKwd = 100m,
            RatePercent = 0m,
            Installments = DemoInstallments(1)
        });
        using var nullInstallment = await CreateLoanAsync(app.LendingClient, bankToken, new
        {
            CustomerCivilId = CustomerId,
            StartDate = "2026-10-01",
            TenorMonths = 1,
            AmountKwd = 100m,
            RatePercent = 0m,
            Installments = new object?[] { null }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, creditWithoutToken.StatusCode);
        Assert.Equal("Bearer", missingToken.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal(HttpStatusCode.Unauthorized, malformedToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSchedule.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullInstallment.StatusCode);
        Assert.Equal(0, await app.CountLoansAsync());
    }

    [Fact]
    public async Task AccessOutageFailsClosedAsDependencyFailure()
    {
        await using var app = await TestApp.StartAsync(accessUnavailable: true);
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");

        using var loan = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, loan.StatusCode);
        Assert.Equal(0, await app.CountLoansAsync());
    }

    [Fact]
    public async Task MissingAndBlockedCustomersCannotReceiveLoans()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");

        using var unknownLoan = await CreateLoanAsync(app.LendingClient, bankToken,
            LoanBody(UnknownCustomerId));
        using var seededEligibility = await CheckEligibilityAsync(app.LendingClient, bankToken, SecondCustomerId);
        using var blockedLoan = await CreateLoanAsync(app.LendingClient, bankToken,
            LoanBody(SecondCustomerId));
        using var unknownBlock = await SetBlockAsync(app.LendingClient, cinetToken, true, UnknownCustomerId);
        using var unknownEligibility = await CheckEligibilityAsync(app.LendingClient, bankToken, UnknownCustomerId);

        Assert.Equal(HttpStatusCode.NotFound, unknownLoan.StatusCode);
        Assert.False((await ReadEligibilityAsync(seededEligibility)).Eligible);
        Assert.Equal(HttpStatusCode.Conflict, blockedLoan.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownBlock.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownEligibility.StatusCode);
        Assert.Equal(0, await app.CountLoansAsync());
        Assert.Empty(await app.FindBlockChangesAsync());
    }

    [Fact]
    public async Task CreditOutageFailsClosedWithoutWritingALoan()
    {
        await using var app = await TestApp.StartAsync(creditUnavailable: true);
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");

        using var loan = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, loan.StatusCode);
        Assert.Equal(0, await app.CountLoansAsync());
    }

    [Fact]
    public async Task LoanCreationWaitsForAConcurrentBlockChange()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        using var initialize = await SetBlockAsync(app.LendingClient, cinetToken, false);
        Assert.Equal(HttpStatusCode.NoContent, initialize.StatusCode);

        await using var connection = new NpgsqlConnection(app.LendingConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync("""
                                      UPDATE customer_loan_blocks SET blocked = true
                                      WHERE customer_civil_id = @CustomerId
                                      """, new { CustomerId }, transaction);

        await using var observer = new NpgsqlConnection(app.LendingConnectionString);
        await observer.OpenAsync(TestContext.Current.CancellationToken);
        var pendingLoan = CreateLoanAsync(app.LendingClient, bankToken);
        var waitedOnRow = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            waitedOnRow = await observer.ExecuteScalarAsync<bool>("""
                                                                  SELECT EXISTS (
                                                                      SELECT 1 FROM pg_stat_activity
                                                                      WHERE datname = current_database()
                                                                        AND wait_event_type = 'Lock'
                                                                        AND query LIKE '%customer_loan_blocks%'
                                                                  )
                                                                  """);
            if (waitedOnRow) break;
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        using var refused = await pendingLoan.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(waitedOnRow, "Loan creation never reached the customer row lock.");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(0, await app.CountLoansAsync());
    }

    [Fact]
    public async Task RepaymentSummaryUsesDueDateAndUploadedPayments()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        using var created = await CreateLoanAsync(app.LendingClient, bankToken, new
        {
            CustomerCivilId = CustomerId,
            StartDate = "2026-10-01",
            TenorMonths = 3,
            AmountKwd = 90m,
            RatePercent = 0m,
            Installments = DemoInstallments(3, 30m)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = Assert.IsType<LoanReceipt>(await created.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));

        app.SetKuwaitDate(new DateOnly(2026, 11, 1));
        var onDueDate = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Empty(onDueDate.DelinquentLoans);
        Assert.Equal(new DateOnly(2026, 11, 1), onDueDate.NextDuePayment?.DueDate);
        Assert.False(onDueDate.NextDuePayment?.Overdue);

        app.SetKuwaitDate(new DateOnly(2026, 11, 2));
        var nextDay = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Equal(loan.Id, Assert.Single(nextDay.DelinquentLoans).LoanId);
        Assert.Equal(30m, nextDay.DelinquentLoans[0].OverdueAmountKwd);
        Assert.True(nextDay.NextDuePayment?.Overdue);

        var firstPayment = new PaymentBody(loan.Id, new DateOnly(2026, 11, 1), 10m, "bank-p-1");
        using var firstUpload = await UploadPaymentsAsync(app.LendingClient, bankToken, [firstPayment]);
        Assert.Equal(HttpStatusCode.OK, firstUpload.StatusCode);

        var beforeSecondPayment = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Equal(20m, Assert.Single(beforeSecondPayment.DelinquentLoans).OverdueAmountKwd);
        Assert.Equal(20m, beforeSecondPayment.NextDuePayment?.RemainingAmountKwd);
        Assert.Single(beforeSecondPayment.LastFivePayments);

        app.SetKuwaitDate(new DateOnly(2026, 11, 4));
        var secondPayment = new PaymentBody(loan.Id, new DateOnly(2026, 11, 3), 20m, "bank-p-2");
        using var secondUpload = await UploadPaymentsAsync(app.LendingClient, bankToken, [secondPayment]);
        Assert.Equal(HttpStatusCode.OK, secondUpload.StatusCode);

        var caughtUp = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Empty(caughtUp.DelinquentLoans);
        Assert.Equal(new DateOnly(2026, 12, 1), caughtUp.NextDuePayment?.DueDate);
        Assert.Equal(30m, caughtUp.NextDuePayment?.RemainingAmountKwd);
        Assert.Equal(["bank-p-2", "bank-p-1"],
            caughtUp.LastFivePayments.Select(payment => payment.ExternalReference));

        using var earlyPayment = await UploadPaymentsAsync(app.LendingClient, bankToken,
            [new PaymentBody(loan.Id, new DateOnly(2026, 11, 4), 30m, "bank-p-3")]);
        Assert.Equal(HttpStatusCode.OK, earlyPayment.StatusCode);
        app.SetKuwaitDate(new DateOnly(2026, 12, 2));
        var afterEarlyPayment = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Empty(afterEarlyPayment.DelinquentLoans);
        Assert.Equal(new DateOnly(2027, 1, 1), afterEarlyPayment.NextDuePayment?.DueDate);
    }

    [Fact]
    public async Task RejectedPaymentBatchesDoNotWriteRows()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        using var created = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = Assert.IsType<LoanReceipt>(await created.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));
        var payment = new PaymentBody(loan.Id, new DateOnly(2026, 11, 1), 1m, "p-1");

        using var cinetUpload = await UploadPaymentsAsync(app.LendingClient, cinetToken, [payment]);
        using var partialBatch = await UploadPaymentsAsync(app.LendingClient, bankToken,
            [payment, payment with { LoanId = Guid.NewGuid(), ExternalReference = "missing-loan" }]);
        using var beforeStart = await UploadPaymentsAsync(app.LendingClient, bankToken,
            [payment with { PaymentDate = new DateOnly(2026, 9, 30) }]);
        using var nullPaymentRequest = new HttpRequestMessage(HttpMethod.Post, "/payments");
        nullPaymentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bankToken);
        nullPaymentRequest.Content = JsonContent.Create(new { Payments = new object?[] { null } });
        using var nullPayment = await app.LendingClient.SendAsync(
            nullPaymentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, cinetUpload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, partialBatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, beforeStart.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullPayment.StatusCode);
        Assert.Equal(0, await app.CountPaymentsAsync());
    }

    [Fact]
    public async Task PaymentReferenceCanBeRetriedButNotReusedForDifferentDetails()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        using var created = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = Assert.IsType<LoanReceipt>(await created.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));
        var payment = new PaymentBody(loan.Id, new DateOnly(2026, 11, 1), 10m, "bank-p-1");

        using var first = await UploadPaymentsAsync(app.LendingClient, bankToken, [payment]);
        using var repeated = await UploadPaymentsAsync(app.LendingClient, bankToken, [payment]);
        using var changed = await UploadPaymentsAsync(app.LendingClient, bankToken,
            [payment with { AmountKwd = 11m }]);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var firstReceipt = Assert.IsType<PaymentUploadReceipt>(
            await first.Content.ReadFromJsonAsync<PaymentUploadReceipt>(TestContext.Current.CancellationToken));
        var repeatedReceipt = Assert.IsType<PaymentUploadReceipt>(
            await repeated.Content.ReadFromJsonAsync<PaymentUploadReceipt>(TestContext.Current.CancellationToken));
        Assert.Equal(firstReceipt.Ids, repeatedReceipt.Ids);
        Assert.Equal(1, await app.CountPaymentsAsync());
    }

    [Fact]
    public async Task BanksCanOnlyReadAndRecordPaymentsForTheirOwnLoans()
    {
        await using var app = await TestApp.StartAsync();
        await app.SeedSecondBankAsync();
        var bankAToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var bankBToken = await LoginAsync(app.AccessClient, "bank-b@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        using var bankACreated = await CreateLoanAsync(app.LendingClient, bankAToken);
        using var bankBCreated = await CreateLoanAsync(app.LendingClient, bankBToken);
        Assert.Equal(HttpStatusCode.Created, bankACreated.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bankBCreated.StatusCode);
        var bankALoan = Assert.IsType<LoanReceipt>(await bankACreated.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));
        var bankBLoan = Assert.IsType<LoanReceipt>(await bankBCreated.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));
        app.SetKuwaitDate(new DateOnly(2026, 11, 2));

        var bankASummary = await ReadSummaryAsync(app.LendingClient, bankAToken);
        var bankBSummary = await ReadSummaryAsync(app.LendingClient, bankBToken);
        var cinetSummary = await ReadSummaryAsync(app.LendingClient, cinetToken);
        using var otherBankPayment = await UploadPaymentsAsync(app.LendingClient, bankBToken,
            [new PaymentBody(bankALoan.Id, new DateOnly(2026, 11, 1), 10m, "other-bank-p-1")]);

        Assert.Equal(bankALoan.Id, Assert.Single(bankASummary.DelinquentLoans).LoanId);
        Assert.Equal(bankALoan.Id, bankASummary.NextDuePayment?.LoanId);
        Assert.Equal(bankBLoan.Id, Assert.Single(bankBSummary.DelinquentLoans).LoanId);
        Assert.Equal(bankBLoan.Id, bankBSummary.NextDuePayment?.LoanId);
        Assert.Equal(2, cinetSummary.DelinquentLoans.Length);
        Assert.Equal(HttpStatusCode.NotFound, otherBankPayment.StatusCode);
        Assert.Equal(0, await app.CountPaymentsAsync());

        using var ownPayment = await UploadPaymentsAsync(app.LendingClient, bankBToken,
            [new PaymentBody(bankBLoan.Id, new DateOnly(2026, 11, 1), 10m, "bank-b-p-1")]);
        Assert.Equal(HttpStatusCode.OK, ownPayment.StatusCode);
        var bankAAfterPayment = await ReadSummaryAsync(app.LendingClient, bankAToken);
        var bankBAfterPayment = await ReadSummaryAsync(app.LendingClient, bankBToken);
        Assert.Empty(bankAAfterPayment.LastFivePayments);
        Assert.Equal("bank-b-p-1", Assert.Single(bankBAfterPayment.LastFivePayments).ExternalReference);
    }

    [Fact]
    public async Task RecentPaymentsReturnsFiveNewestByPaymentDate()
    {
        await using var app = await TestApp.StartAsync();
        var bankToken = await LoginAsync(app.AccessClient, "bank@example.test", "bank-local");
        var cinetToken = await LoginAsync(app.AccessClient, "cinet@example.test", "cinet-local");
        using var created = await CreateLoanAsync(app.LendingClient, bankToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = Assert.IsType<LoanReceipt>(await created.Content.ReadFromJsonAsync<LoanReceipt>(
            TestContext.Current.CancellationToken));

        var sixPayments = Enumerable.Range(1, 6)
            .Select(day => new PaymentBody(loan.Id, new DateOnly(2026, 11, day), 1m, $"p-{day}"))
            .ToArray();
        using var uploaded = await UploadPaymentsAsync(app.LendingClient, bankToken, sixPayments);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        app.SetKuwaitDate(new DateOnly(2026, 11, 6));
        var summary = await ReadSummaryAsync(app.LendingClient, cinetToken);
        Assert.Equal(6, await app.CountPaymentsAsync());
        Assert.Equal(["p-6", "p-5", "p-4", "p-3", "p-2"],
            summary.LastFivePayments.Select(item => item.ExternalReference));
    }

    [Fact]
    public async Task OpenApiDocumentsProtectedLendingRoutes()
    {
        await using var app = await TestApp.StartAsync();

        using var response =
            await app.LendingClient.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document =
            await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken);
        Assert.NotNull(document);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/loans").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/loan-blocks").GetProperty("put")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/customers/eligibility/check").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/payments").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(paths.GetProperty("/customers/repayments/summary").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));

        using var creditResponse = await app.CreditClient.GetAsync("/openapi/v1.json",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, creditResponse.StatusCode);
        using var creditDocument = await creditResponse.Content.ReadFromJsonAsync<JsonDocument>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(creditDocument);
        Assert.True(creditDocument.RootElement.GetProperty("paths")
            .GetProperty("/customers/lookup").GetProperty("post")
            .GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/login",
            new { UserName = username, Password = password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginReceipt>(TestContext.Current.CancellationToken);
        return Assert.IsType<LoginReceipt>(login).AccessToken;
    }

    private static async Task<ActorReceipt> ActorAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/actor");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ActorReceipt>(await response.Content.ReadFromJsonAsync<ActorReceipt>(
            TestContext.Current.CancellationToken));
    }

    private static object LoanBody(string customerId) => new
    {
        CustomerCivilId = customerId,
        StartDate = "2026-10-01",
        TenorMonths = 24,
        AmountKwd = 1000.125m,
        RatePercent = 7.5m,
        Installments = DemoInstallments(24)
    };

    private static InstallmentBody[] DemoInstallments(int months, decimal amountKwd = 50m) =>
        Enumerable.Range(1, months)
            .Select(month => new InstallmentBody(new DateOnly(2026, 10, 1).AddMonths(month), amountKwd))
            .ToArray();

    private static async Task<HttpResponseMessage> CreateLoanAsync(HttpClient client, string? token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/loans");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body ?? LoanBody(CustomerId));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SetBlockAsync(
        HttpClient client, string token, bool blocked, string customerId = CustomerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/loan-blocks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { CustomerCivilId = customerId, Blocked = blocked });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> UploadPaymentsAsync(
        HttpClient client, string token, PaymentBody[] payments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { Payments = payments });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<RepaymentSummaryReceipt> ReadSummaryAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/customers/repayments/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { CustomerCivilId = CustomerId });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<RepaymentSummaryReceipt>(await response.Content.ReadFromJsonAsync<RepaymentSummaryReceipt>(
            TestContext.Current.CancellationToken));
    }

    private static async Task<HttpResponseMessage> CheckEligibilityAsync(
        HttpClient client, string? token, string customerId = CustomerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/customers/eligibility/check");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { CustomerCivilId = customerId });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<EligibilityReceipt> ReadEligibilityAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<EligibilityReceipt>(await response.Content.ReadFromJsonAsync<EligibilityReceipt>(
            TestContext.Current.CancellationToken));
    }

    private static async Task<HttpResponseMessage> LookupCustomerAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/customers/lookup");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { CustomerCivilId = CustomerId });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record LoginReceipt(string AccessToken);

    private sealed record ActorReceipt(Guid ActorId, Guid InstitutionId);

    private sealed record LoanReceipt(Guid Id);

    private sealed record EligibilityReceipt(bool Eligible);

    private sealed record InstallmentBody(DateOnly DueDate, decimal AmountKwd);

    private sealed record PaymentBody(Guid LoanId, DateOnly PaymentDate, decimal AmountKwd, string ExternalReference);

    private sealed record PaymentUploadReceipt(Guid[] Ids);

    private sealed record RepaymentSummaryReceipt(
        DelinquentLoanReceipt[] DelinquentLoans,
        DuePaymentReceipt? NextDuePayment,
        RecordedPaymentReceipt[] LastFivePayments);

    private sealed record DelinquentLoanReceipt(Guid LoanId, decimal OverdueAmountKwd);

    private sealed record DuePaymentReceipt(Guid LoanId, DateOnly DueDate, decimal RemainingAmountKwd, bool Overdue);

    private sealed record RecordedPaymentReceipt(string ExternalReference);

    private sealed record BlockChange(bool Blocked, Guid ChangedBy, DateTime ChangedAt);

    private sealed record LoanRow(
        Guid Id,
        string CustomerCivilId,
        Guid InstitutionId,
        DateOnly StartDate,
        int TenorMonths,
        decimal AmountKwd,
        decimal RatePercent,
        string Status);

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _accessDatabase;
        private readonly string _creditDatabase;
        private readonly string _lendingDatabase;
        private readonly string _lendingConnectionString;
        private readonly AccessFactory _accessFactory;
        private readonly CreditFactory _creditFactory;
        private readonly LendingFactory _lendingFactory;
        private readonly TestClock _clock;

        private TestApp(string adminConnectionString, string accessDatabase, string creditDatabase,
            string lendingDatabase, string lendingConnectionString, AccessFactory accessFactory,
            CreditFactory creditFactory, LendingFactory lendingFactory, TestClock clock,
            HttpClient accessClient, HttpClient creditClient, HttpClient lendingClient)
        {
            _adminConnectionString = adminConnectionString;
            _accessDatabase = accessDatabase;
            _creditDatabase = creditDatabase;
            _lendingDatabase = lendingDatabase;
            _lendingConnectionString = lendingConnectionString;
            _accessFactory = accessFactory;
            _creditFactory = creditFactory;
            _lendingFactory = lendingFactory;
            _clock = clock;
            AccessClient = accessClient;
            CreditClient = creditClient;
            LendingClient = lendingClient;
        }

        public HttpClient AccessClient { get; }
        public HttpClient CreditClient { get; }
        public HttpClient LendingClient { get; }
        public string LendingConnectionString => _lendingConnectionString;

        public void SetKuwaitDate(DateOnly date) => _clock.KuwaitDate = date;

        public async Task SeedSecondBankAsync()
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
                { Database = _accessDatabase }.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var institutionId = Guid.CreateVersion7();
            var userId = Guid.CreateVersion7();
            await connection.ExecuteAsync("""
                                          INSERT INTO institutions (id, code, name, kind)
                                          VALUES (@InstitutionId, 'BANK_B', 'Bank B', 'Bank')
                                          """, new { InstitutionId = institutionId });
            await connection.ExecuteAsync("""
                                          INSERT INTO staff_users (id, username, password_hash, institution_id)
                                          SELECT @UserId, 'bank-b@example.test', password_hash, @InstitutionId
                                          FROM staff_users WHERE username = 'bank@example.test'
                                          """, new { UserId = userId, InstitutionId = institutionId });
            await connection.ExecuteAsync("""
                                          INSERT INTO staff_user_roles (staff_user_id, role)
                                          VALUES (@UserId, 'CustomerReader'), (@UserId, 'LoanCreator'),
                                                 (@UserId, 'PaymentWriter')
                                          """, new { UserId = userId });
        }

        public static async Task<TestApp> StartAsync(bool accessUnavailable = false, bool creditUnavailable = false)
        {
            var adminConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
                                        ??
                                        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=local-demo";
            var accessDatabase = $"test_access_{Guid.NewGuid():N}";
            var creditDatabase = $"test_credit_{Guid.NewGuid():N}";
            var lendingDatabase = $"test_lending_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync(TestContext.Current.CancellationToken);
                // NOTE(aalhendi): These SQL identifiers contain only generated GUIDs.
                await admin.ExecuteAsync($"CREATE DATABASE \"{accessDatabase}\"");
                await admin.ExecuteAsync($"CREATE DATABASE \"{creditDatabase}\"");
                await admin.ExecuteAsync($"CREATE DATABASE \"{lendingDatabase}\"");
            }

            var accessConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
                { Database = accessDatabase }.ConnectionString;
            var creditConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
                { Database = creditDatabase }.ConnectionString;
            var lendingConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
                { Database = lendingDatabase }.ConnectionString;
            var accessFactory = new AccessFactory(accessConnectionString);
            var accessClient = accessFactory.CreateClient();
            var creditFactory = new CreditFactory(creditConnectionString, accessFactory);
            var creditClient = creditFactory.CreateClient();
            var clock = new TestClock();
            var lendingFactory = new LendingFactory(lendingConnectionString, accessFactory, creditFactory,
                clock, accessUnavailable, creditUnavailable);
            var lendingClient = lendingFactory.CreateClient();
            return new TestApp(adminConnectionString, accessDatabase, creditDatabase, lendingDatabase,
                lendingConnectionString, accessFactory, creditFactory, lendingFactory, clock,
                accessClient, creditClient, lendingClient);
        }

        public async Task<LoanRow> FindLoanAsync(Guid id)
        {
            await using var connection = new NpgsqlConnection(_lendingConnectionString);
            return await connection.QuerySingleAsync<LoanRow>("""
                                                              SELECT id AS "Id", customer_civil_id AS "CustomerCivilId",
                                                                     institution_id AS "InstitutionId", start_date AS "StartDate",
                                                                     tenor_months AS "TenorMonths", amount_kwd AS "AmountKwd",
                                                                     rate_percent AS "RatePercent", status AS "Status"
                                                              FROM loans WHERE id = @Id
                                                              """, new { Id = id });
        }

        public async Task<int> CountLoansAsync()
        {
            await using var connection = new NpgsqlConnection(_lendingConnectionString);
            return await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM loans");
        }

        public async Task<int> CountPaymentsAsync()
        {
            await using var connection = new NpgsqlConnection(_lendingConnectionString);
            return await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM loan_payments");
        }

        public async Task<IReadOnlyList<BlockChange>> FindBlockChangesAsync()
        {
            await using var connection = new NpgsqlConnection(_lendingConnectionString);
            return (await connection.QueryAsync<BlockChange>("""
                                                             SELECT blocked AS "Blocked", changed_by AS "ChangedBy",
                                                                    changed_at AS "ChangedAt"
                                                             FROM loan_block_changes ORDER BY changed_at, id
                                                             """)).ToList();
        }

        public async ValueTask DisposeAsync()
        {
            LendingClient.Dispose();
            CreditClient.Dispose();
            AccessClient.Dispose();
            await _lendingFactory.DisposeAsync();
            await _creditFactory.DisposeAsync();
            await _accessFactory.DisposeAsync();
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.ExecuteAsync($"DROP DATABASE \"{_lendingDatabase}\" WITH (FORCE)");
            await admin.ExecuteAsync($"DROP DATABASE \"{_creditDatabase}\" WITH (FORCE)");
            await admin.ExecuteAsync($"DROP DATABASE \"{_accessDatabase}\" WITH (FORCE)");
        }

        private sealed class AccessFactory(string connectionString) : WebApplicationFactory<AccessApi::Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder) =>
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:Access"] = connectionString }));
        }

        private sealed class CreditFactory(string connectionString, AccessFactory accessFactory)
            : WebApplicationFactory<CreditApi::Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Credit"] = connectionString,
                        ["Access:BaseUrl"] = "http://access.test/"
                    }));
                builder.ConfigureTestServices(services => services.AddHttpClient("Access")
                    .ConfigurePrimaryHttpMessageHandler(() => accessFactory.Server.CreateHandler()));
            }
        }

        private sealed class LendingFactory(
            string connectionString,
            AccessFactory accessFactory,
            CreditFactory creditFactory,
            TestClock clock,
            bool accessUnavailable,
            bool creditUnavailable)
            : WebApplicationFactory<LendingApi::Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock));
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Lending"] = connectionString,
                        ["Access:BaseUrl"] = accessUnavailable ? "http://127.0.0.1:1/" : "http://access.test/",
                        ["Credit:BaseUrl"] = creditUnavailable ? "http://127.0.0.1:1/" : "http://credit.test/"
                    }));
                if (!accessUnavailable)
                    builder.ConfigureTestServices(services => services.AddHttpClient("Access")
                        .ConfigurePrimaryHttpMessageHandler(() => accessFactory.Server.CreateHandler()));
                if (!creditUnavailable)
                    builder.ConfigureTestServices(services => services.AddHttpClient("Credit")
                        .ConfigurePrimaryHttpMessageHandler(() => creditFactory.Server.CreateHandler()));
            }
        }

        private sealed class TestClock : TimeProvider
        {
            public DateOnly? KuwaitDate { get; set; }

            public override DateTimeOffset GetUtcNow() => KuwaitDate is { } date
                ? new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero)
                : throw new InvalidOperationException("Set the test date before reading the repayment summary.");
        }
    }
}