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

        var command = new CreateLoanCommand(customerId, startDate, tenor, principal, financingRate);
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