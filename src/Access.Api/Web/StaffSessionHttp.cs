using Access.Api.Application;
using Access.Api.Domain;

namespace Access.Api.Web;

internal sealed record StaffSession(StaffSessionToken Token, StaffActor Actor);

internal static class StaffSessionHttp
{
    public const string Scheme = "Bearer";

    public static async Task<StaffSession?> ReadAsync(HttpContext context, AccessService service)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        if (StaffSessionToken.Parse(authorization[7..]).Value is not StaffSessionToken token) return null;

        var actor = await service.ResolveAsync(token);
        return actor is null ? null : new StaffSession(token, actor);
    }

    public static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = Scheme;
        return Results.Unauthorized();
    }
}