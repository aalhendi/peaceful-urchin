using Lending.Api.Application;
using Lending.Api.Domain;
using Lending.Api.Infrastructure;
using Lending.Api.Web.Contracts;
using Shared.Vocabulary;

namespace Lending.Api.Web;

internal static class LendingHandlers
{
    internal static string Live() => "alive";

    internal static async Task<IResult> CreateLoanAsync(
        CreateLoanRequest request, HttpContext context, AccessActorClient access, LendingService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return ActorError(context, actor);

        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId ||
            request.StartDate is not DateOnly startDate ||
            request.TenorMonths is not int months || LoanTenor.Parse(months).Value is not LoanTenor tenor ||
            request.AmountKwd is not decimal amount ||
            LoanPrincipal.Parse(amount).Value is not LoanPrincipal principal ||
            request.RatePercent is not decimal rate ||
            FinancingRate.Parse(rate).Value is not FinancingRate financingRate)
            return Results.BadRequest("Invalid loan information.");

        if (request.Installments is null ||
            request.Installments.Any(installment =>
                installment is null || installment.DueDate is null || installment.AmountKwd is null))
            return Results.BadRequest("A repayment schedule is required.");
        var drafts = request.Installments.Select(installment =>
            new InstallmentDraft(installment.DueDate!.Value, installment.AmountKwd!.Value)).ToArray();
        if (RepaymentSchedule.Parse(startDate, tenor, drafts).Value is not RepaymentSchedule schedule)
            return Results.BadRequest("Invalid repayment schedule.");

        var command = new CreateLoanCommand(customerId, startDate, tenor, principal, financingRate, schedule);
        return (await service.CreateLoanAsync(
                resolved.Actor, resolved.Credential, command, context.RequestAborted)) switch
            {
                LoanCreated created => Results.Json(new LoanCreatedResponse(created.Id.Value), statusCode: 201),
                CustomerBlocked => Results.Conflict("Customer is blocked from receiving a loan."),
                CustomerIneligible => Results.Conflict("Customer is not eligible for a loan."),
                CustomerNotFound => Results.NotFound(),
                LoanUnauthorized => Unauthorized(context),
                LoanDependencyUnavailable => Results.StatusCode(503),
                LoanForbidden => Results.StatusCode(403)
            };
    }

    internal static async Task<IResult> SetLoanBlockAsync(
        SetLoanBlockRequest request, HttpContext context, AccessActorClient access, LendingService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return ActorError(context, actor);

        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId ||
            request.Blocked is not bool blocked)
            return Results.BadRequest("Invalid loan block information.");

        return (await service.SetLoanBlockAsync(
                resolved.Actor, resolved.Credential, customerId, blocked, context.RequestAborted)) switch
            {
                LoanBlockChanged => Results.NoContent(),
                CustomerNotFound => Results.NotFound(),
                LoanUnauthorized => Unauthorized(context),
                LoanForbidden => Results.StatusCode(403),
                LoanDependencyUnavailable => Results.StatusCode(503)
            };
    }

    internal static async Task<IResult> UploadPaymentsAsync(
        UploadPaymentsRequest request, HttpContext context, AccessActorClient access, PaymentService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return ActorError(context, actor);
        if (request.Payments is not { Length: > 0 and <= 100 })
            return Results.BadRequest("Provide 1 to 100 payments.");

        var payments = new List<PaymentDraft>(request.Payments.Length);
        foreach (var item in request.Payments)
        {
            if (item is null || item.LoanId is not Guid loanIdValue ||
                LoanId.Parse(loanIdValue).Value is not LoanId loanId ||
                item.PaymentDate is not DateOnly paymentDate ||
                item.AmountKwd is not decimal amountValue ||
                PositiveKwdAmount.Parse(amountValue).Value is not PositiveKwdAmount amount ||
                PaymentReference.Parse(item.ExternalReference).Value is not PaymentReference reference)
                return Results.BadRequest("Invalid payment information.");
            payments.Add(new PaymentDraft(loanId, paymentDate, amount, reference));
        }

        context.Response.Headers.CacheControl = "no-store";
        return await service.UploadAsync(resolved.Actor, payments) switch
        {
            PaymentsRecorded recorded => Results.Ok(new UploadPaymentsResponse(
                recorded.Ids.Select(id => id.Value).ToArray())),
            PaymentLoanMissing => Results.NotFound(),
            PaymentReferenceConflict => Results.Conflict("Payment reference was already used for different data."),
            PaymentBeforeLoanStart => Results.BadRequest("Payment date precedes the loan start date."),
            LoanForbidden => Results.StatusCode(403)
        };
    }

    internal static async Task<IResult> ReadRepaymentSummaryAsync(
        RepaymentSummaryRequest request, HttpContext context, AccessActorClient access,
        RepaymentQueryService service, TimeProvider clock)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return ActorError(context, actor);
        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        context.Response.Headers.CacheControl = "no-store";
        return await service.ReadAsync(resolved.Actor, customerId, today) switch
        {
            RepaymentSummaryFound found => Results.Ok(new RepaymentSummaryResponse(
                found.Summary.DelinquentLoans.Select(loan =>
                    new DelinquentLoanResponse(loan.LoanId.Value, loan.OverdueAmount.Dinars)).ToArray(),
                found.Summary.NextDuePayment is { } due
                    ? new DuePaymentResponse(due.LoanId.Value, due.DueDate, due.RemainingAmount.Dinars, due.Overdue)
                    : null,
                found.Summary.LastFivePayments.Select(payment => new RecordedPaymentResponse(
                    payment.Id.Value, payment.LoanId.Value, payment.PaymentDate,
                    payment.Amount.Dinars, payment.Reference.Value)).ToArray())),
            LoanForbidden => Results.StatusCode(403)
        };
    }

    private static async Task<ActorLookupResult> ReadActorAsync(HttpContext context, AccessActorClient access)
    {
        if (context.Request.Headers.Authorization.Count != 1)
            return new ActorUnauthorized();
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            authorization.Length is <= 7 or > 519)
            return new ActorUnauthorized();

        if (BearerCredential.Parse(authorization[7..]) is not BearerCredential credential)
            return new ActorUnauthorized();
        return await access.ResolveAsync(credential, context.RequestAborted);
    }

    private static IResult ActorError(HttpContext context, ActorLookupResult result) => result switch
    {
        ActorUnauthorized => Unauthorized(context),
        ActorUnavailable => Results.StatusCode(503),
        ActorResolved => throw new InvalidOperationException("Expected an actor lookup error.")
    };

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Unauthorized();
    }
}