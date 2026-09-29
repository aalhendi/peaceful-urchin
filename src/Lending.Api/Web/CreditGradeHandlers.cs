using Lending.Api.Application;
using Lending.Api.Domain;
using Lending.Api.Infrastructure;
using Lending.Api.Web.Contracts;
using Shared.Vocabulary;

namespace Lending.Api.Web;

internal static class CreditGradeHandlers
{
    internal static async Task<IResult> ReadAsync(
        CreditGradeRequest request, HttpContext context, AccessActorClient access,
        CreditGradeService service, TimeProvider clock)
    {
        var actor = await LendingActorHttp.ReadAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return LendingActorHttp.Error(context, actor);
        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        context.Response.Headers.CacheControl = "no-store";
        return await service.ReadAsync(
                resolved.Actor, resolved.Credential, customerId, today, context.RequestAborted) switch
            {
                CreditGradeKnown known => Results.Ok(new CreditGradeResponse(known.Decision switch
                {
                    GradeA => CreditGradeValue.A,
                    GradeB => CreditGradeValue.B,
                    GradeC => CreditGradeValue.C,
                    GradeF => CreditGradeValue.F,
                    PendingReview => CreditGradeValue.PendingReview
                }, today)),
                CustomerNotFound => Results.NotFound(),
                LoanUnauthorized => LendingActorHttp.Unauthorized(context),
                LoanForbidden => Results.StatusCode(403),
                LoanDependencyUnavailable => Results.StatusCode(503)
            };
    }
}