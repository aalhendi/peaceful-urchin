using Lending.Api.Application;
using Lending.Api.Domain;
using Lending.Api.Infrastructure;
using Lending.Api.Web.Contracts;

namespace Lending.Api.Web;

internal static class LitigationHandlers
{
    internal static async Task<IResult> OpenAsync(
        Guid loanId, OpenLitigationRequest request, HttpContext context, AccessActorClient access,
        LitigationService service)
    {
        var actor = await LendingActorHttp.ReadAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return LendingActorHttp.Error(context, actor);
        if (LoanId.Parse(loanId).Value is not LoanId parsedLoanId ||
            CourtId.Parse(request.CourtId).Value is not CourtId courtId)
            return Results.BadRequest("Invalid litigation information.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.OpenAsync(resolved.Actor, parsedLoanId, courtId) switch
        {
            LitigationOpened opened => Results.Json(new LitigationOpenedResponse(opened.Id.Value), statusCode: 201),
            LitigationNotFound => Results.NotFound(),
            LitigationConflict => Results.Conflict("Court ID already exists for this loan."),
            LitigationForbidden => Results.StatusCode(403)
        };
    }

    internal static async Task<IResult> ReadAsync(
        Guid loanId, HttpContext context, AccessActorClient access, LitigationService service)
    {
        var actor = await LendingActorHttp.ReadAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return LendingActorHttp.Error(context, actor);
        if (LoanId.Parse(loanId).Value is not LoanId parsedLoanId)
            return Results.BadRequest("Invalid loan ID.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.ReadAsync(resolved.Actor, parsedLoanId) switch
        {
            LitigationRead found => Results.Ok(new LoanLitigationResponse(
                found.Loan.LoanId.Value, found.Loan.CustomerId.ExposeSecret(),
                found.Loan.InstitutionId.Value,
                found.Loan.Cases.Select(ToResponse).ToArray())),
            LitigationNotFound => Results.NotFound(),
            LitigationForbidden => Results.StatusCode(403)
        };
    }

    internal static async Task<IResult> RecordVerdictAsync(
        Guid litigationId, RecordVerdictRequest request, HttpContext context, AccessActorClient access,
        LitigationService service, TimeProvider clock)
    {
        var actor = await LendingActorHttp.ReadAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return LendingActorHttp.Error(context, actor);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        if (LitigationId.Parse(litigationId).Value is not LitigationId parsedId ||
            LitigationStates.ParseVerdict(request.Status, request.VerdictDate, today).Value is not Verdict verdict)
            return Results.BadRequest("Invalid verdict information.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.RecordVerdictAsync(resolved.Actor, parsedId, verdict) switch
        {
            VerdictRecorded => Results.NoContent(),
            LitigationNotFound => Results.NotFound(),
            LitigationConflict => Results.Conflict("A different verdict was already recorded."),
            VerdictBeforeLoanStart => Results.BadRequest("Verdict date precedes the loan start date."),
            LitigationForbidden => Results.StatusCode(403)
        };
    }

    private static LitigationCaseResponse ToResponse(LitigationCase item)
    {
        var (status, verdictDate) = item.State switch
        {
            Pending => ("Pending", (DateOnly?)null),
            Guilty guilty => ("Guilty", guilty.VerdictDate),
            Innocent innocent => ("Innocent", innocent.VerdictDate)
        };
        return new LitigationCaseResponse(item.Id.Value, item.CourtId.Value, status, verdictDate);
    }
}