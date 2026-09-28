using Credit.Api.Web.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Credit.Api.Web;

internal static class Routes
{
    public static void MapRoutes(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.MapGet("/health/live", CreditHandlers.Live);
        app.MapPost("/customers/eligibility/check", CreditHandlers.CheckEligibilityAsync)
            .DocumentBearerToken()
            .Produces<EligibilityResponse>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(503);
    }

    private static RouteHandlerBuilder DocumentBearerToken(this RouteHandlerBuilder route) =>
        route.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
            return Task.CompletedTask;
        });
}