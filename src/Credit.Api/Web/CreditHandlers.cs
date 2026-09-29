using Credit.Api.Application;
using Credit.Api.Domain;
using Credit.Api.Infrastructure;
using Credit.Api.Web.Contracts;
using Shared.Vocabulary;

namespace Credit.Api.Web;

internal static class CreditHandlers
{
    internal static string Live() => "alive";

    internal static async Task<IResult> LookupCustomerAsync(
        CustomerIdRequest request, HttpContext context, AccessActorClient access, CustomerService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor is not ActorResolved resolved) return ActorError(context, actor);

        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.FindCustomerAsync(resolved.Actor, customerId) switch
        {
            CustomerFound => Results.NoContent(),
            CustomerMissing => Results.NotFound(),
            CustomerForbidden => Results.StatusCode(403)
        };
    }

    internal static async Task<IResult> ReadProfileAsync(
        CustomerIdRequest request, HttpContext context, AccessActorClient access, CustomerService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor is not ActorResolved resolved) return ActorError(context, actor);
        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.ReadProfileAsync(resolved.Actor, customerId) switch
        {
            CustomerProfileFound found => Results.Ok(new CustomerProfileResponse(
                found.Customer.Id.ExposeSecret(), found.Customer.Name.Value, found.Customer.DateOfBirth)),
            CustomerMissing => Results.NotFound(),
            CustomerForbidden => Results.StatusCode(403)
        };
    }

    internal static async Task<IResult> UpdateCustomerNameAsync(
        UpdateCustomerNameRequest request, HttpContext context, AccessActorClient access, CustomerService service)
    {
        var actor = await ReadActorAsync(context, access);
        if (actor is not ActorResolved resolved) return ActorError(context, actor);
        if (CivilId.Parse(request.CustomerCivilId).Value is not CivilId customerId)
            return Results.BadRequest("Invalid customer Civil ID.");
        if (CustomerName.Parse(request.Name).Value is not CustomerName name)
            return Results.BadRequest("Invalid customer name.");

        context.Response.Headers.CacheControl = "no-store";
        return await service.UpdateNameAsync(resolved.Actor, customerId, name) switch
        {
            CustomerNameUpdated => Results.NoContent(),
            CustomerMissing => Results.NotFound(),
            CustomerForbidden => Results.StatusCode(403)
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

        return await access.ResolveAsync(authorization[7..], context.RequestAborted);
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