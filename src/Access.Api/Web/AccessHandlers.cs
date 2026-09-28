using System.Collections.Immutable;
using Access.Api.Application;
using Access.Api.Domain;
using Access.Api.Web.Contracts;

namespace Access.Api.Web;

internal static class AccessHandlers
{
    internal static string Live() => "alive";

    internal static async Task<IResult> LoginAsync(
        LoginRequest request, HttpContext context, AccessService service)
    {
        if (StaffUserName.Parse(request.UserName).Value is not StaffUserName userName ||
            StaffPassword.Parse(request.Password).Value is not StaffPassword password)
            return Results.Unauthorized();

        var login = await service.LoginAsync(userName, password);
        if (login is null) return Results.Unauthorized();

        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new LoginResponse(
            StaffSessionHttp.Scheme, login.Token.ExposeSecret(), login.ExpiresAt));
    }

    internal static async Task<IResult> GetActor(HttpContext context, AccessService service)
    {
        var session = await StaffSessionHttp.ReadAsync(context, service);
        if (session is null) return StaffSessionHttp.Unauthorized(context);
        var actor = session.Actor;

        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new ActorResponse(
            actor.Id.Value, actor.InstitutionId.Value, actor.InstitutionKind.Value,
            actor.Roles.Select(role => role.Code).ToArray(),
            actor.Roles.Select(role => role.Permission).ToArray()));
    }

    internal static async Task<IResult> LogoutAsync(HttpContext context, AccessService service)
    {
        var session = await StaffSessionHttp.ReadAsync(context, service);
        if (session is null) return StaffSessionHttp.Unauthorized(context);
        if (!await service.LogoutAsync(session.Token)) return StaffSessionHttp.Unauthorized(context);
        return Results.NoContent();
    }

    internal static async Task<IResult> ReplaceRolesAsync(
        Guid userId, ReplaceRolesRequest request, HttpContext context, AccessService service)
    {
        var session = await StaffSessionHttp.ReadAsync(context, service);
        if (session is null) return StaffSessionHttp.Unauthorized(context);
        var actor = session.Actor;
        if (StaffUserId.Parse(userId).Value is not StaffUserId targetId)
            return Results.BadRequest("Invalid staff user ID.");
        if (request.Roles is null) return Results.BadRequest("Roles are required.");
        if (request.Roles.Length > 5) return Results.BadRequest("Provide at most five roles.");

        var roles = ImmutableArray.CreateBuilder<StaffRole>();
        var seen = new HashSet<StaffRole>();
        foreach (var value in request.Roles)
        {
            if (StaffRole.Parse(value).Value is not StaffRole role)
                return Results.BadRequest("Unknown staff role.");
            if (!seen.Add(role)) return Results.BadRequest("Duplicate staff role.");
            roles.Add(role);
        }

        return await service.ChangeRolesAsync(actor, targetId, roles.ToImmutable()) switch
        {
            ChangeRolesResult.Changed => Results.NoContent(),
            ChangeRolesResult.Forbidden => Results.StatusCode(403),
            ChangeRolesResult.NotFound => Results.NotFound(),
            ChangeRolesResult.InvalidRoles => Results.BadRequest("Role is not allowed for that institution."),
            _ => throw new InvalidOperationException("Unknown role change result.")
        };
    }
}