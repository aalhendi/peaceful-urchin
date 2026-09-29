using Lending.Api.Domain;
using Lending.Api.Infrastructure;

namespace Lending.Api.Web;

internal static class LendingActorHttp
{
    internal static async Task<ActorLookupResult> ReadAsync(HttpContext context, AccessActorClient access)
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

    internal static IResult Error(HttpContext context, ActorLookupResult result) => result switch
    {
        ActorUnauthorized => Unauthorized(context),
        ActorUnavailable => Results.StatusCode(503),
        ActorResolved => throw new InvalidOperationException("Expected an actor lookup error.")
    };

    internal static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Unauthorized();
    }
}