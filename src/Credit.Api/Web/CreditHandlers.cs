using Credit.Api.Application;
using Credit.Api.Infrastructure;
using Credit.Api.Web.Contracts;
using Shared.Vocabulary;

namespace Credit.Api.Web;

internal static class CreditHandlers
{
    internal static string Live() => "alive";

    internal static async Task<IResult> CheckEligibilityAsync(
        EligibilityRequest request, HttpContext context, AccessActorClient access, CustomerService service)
    {
        if (context.Request.Headers.Authorization.Count != 1)
            return Unauthorized(context);
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            authorization.Length is <= 7 or > 519)
            return Unauthorized(context);

        var actor = await access.ResolveAsync(authorization[7..], context.RequestAborted);
        if (actor is not ActorResolved resolved)
            return actor switch
            {
                ActorUnauthorized => Unauthorized(context),
                ActorUnavailable => Results.StatusCode(503),
                ActorResolved => throw new InvalidOperationException("Expected an actor lookup error.")
            };

        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.CheckLoanEligibilityAsync(resolved.Actor, customerId) switch
        {
            EligibilityKnown known => Results.Ok(new EligibilityResponse(known.Eligible)),
            CustomerMissing => Results.NotFound(),
            EligibilityForbidden => Results.StatusCode(403)
        };
    }

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Unauthorized();
    }
}