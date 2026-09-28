using Credit.Api.Application;
using Credit.Api.Infrastructure;
using Credit.Api.Web;
using Database.Migrations;
using Microsoft.OpenApi;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer" }
        };
        return Task.CompletedTask;
    });
});
builder.Services.AddSingleton(serviceProvider =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Credit")
                           ?? throw new InvalidOperationException("ConnectionStrings:Credit is required.");
    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<ICustomerStore, CustomerStore>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddHttpClient("Access", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Access:BaseUrl"]
                                 ?? throw new InvalidOperationException("Access:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddScoped<AccessActorClient>();

var app = builder.Build();

var creditConnectionString = app.Configuration.GetConnectionString("Credit")
                             ?? throw new InvalidOperationException("ConnectionStrings:Credit is required.");
foreach (var name in await MigrationRunner.ApplyAsync(creditConnectionString, typeof(Program).Assembly,
             "Credit.Api.Migrations."))
    app.Logger.LogInformation("Applied database migration {Migration}", name);

app.MapRoutes();

app.Run();

public partial class Program;