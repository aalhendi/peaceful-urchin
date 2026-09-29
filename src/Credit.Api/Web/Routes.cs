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
        app.MapPost("/customers/lookup", CreditHandlers.LookupCustomerAsync)
            .DocumentBearerToken()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(503);

        // NOTE(aalhendi): QUERY keeps the Civil ID in the body and marks this as a safe read.
        // TODO(aalhendi): Not all HTTP Clients support QUERY. Can add a deprecated fallback. Depends on SLA
        app.MapMethods("/customers/profile", ["QUERY"], CreditHandlers.ReadProfileAsync)
            .DocumentBearerToken()
            .Produces<CustomerProfileResponse>()
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(503);

        app.MapPost("/customers/profile", CreditHandlers.UpdateCustomerNameAsync)
            .DocumentBearerToken()
            .Produces(204)
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