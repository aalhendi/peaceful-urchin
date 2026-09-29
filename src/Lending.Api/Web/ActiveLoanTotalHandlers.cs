using Lending.Api.Application;
using Lending.Api.Domain;
using Lending.Api.Infrastructure;
using Lending.Api.Web.Contracts;
using Shared.Vocabulary;

namespace Lending.Api.Web;

internal static class ActiveLoanTotalHandlers
{
    internal static async Task<IResult> ReadAsync(
        ActiveLoanTotalRequest request, HttpContext context, AccessActorClient access,
        ActiveLoanTotalService service)
    {
        var actor = await LendingActorHttp.ReadAsync(context, access);
        if (actor.Value is not ActorResolved resolved) return LendingActorHttp.Error(context, actor);
        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.ReadAsync(resolved.Actor, customerId) switch
        {
            ActiveLoanTotalFound found => Results.Ok(new ActiveLoanTotalResponse(
                found.Total.LoanCount, found.Total.OriginalPrincipalTotal.Dinars)),
            LoanForbidden => Results.StatusCode(403)
        };
    }
}