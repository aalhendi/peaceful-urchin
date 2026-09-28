using Access.Api.Web.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Access.Api.Web;

internal static class Routes
{
    public static void MapRoutes(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.MapGet("/health/live", AccessHandlers.Live);

        app.MapPost("/auth/login", AccessHandlers.LoginAsync)
            .RequireRateLimiting("login")
            .Produces<LoginResponse>()
            .Produces(401)
            .Produces(429);

        app.MapGet("/auth/actor", AccessHandlers.GetActor)
            .DocumentBearerToken()
            .Produces<ActorResponse>()
            .Produces(401);

        app.MapPost("/auth/logout", AccessHandlers.LogoutAsync)
            .DocumentBearerToken()
            .Produces(204)
            .Produces(401);

        app.MapPut("/staff-users/{userId:guid}/roles", AccessHandlers.ReplaceRolesAsync)
            .DocumentBearerToken()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404);
    }

    private static RouteHandlerBuilder DocumentBearerToken(this RouteHandlerBuilder route) =>
        route.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(StaffSessionHttp.Scheme, context.Document)] = []
            });
            return Task.CompletedTask;
        });
}