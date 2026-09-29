using Database.Migrations;
using Lending.Api.Application;
using Lending.Api.Infrastructure;
using Lending.Api.Web;
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
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Lending")
                           ?? throw new InvalidOperationException("ConnectionStrings:Lending is required.");
    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<ILendingStore, LendingStore>();
builder.Services.AddSingleton<IPaymentStore, PaymentStore>();
builder.Services.AddSingleton<IRepaymentQueryStore, RepaymentQueryStore>();
builder.Services.AddSingleton<ILitigationStore, LitigationStore>();
builder.Services.AddSingleton<IActiveLoanTotalStore, ActiveLoanTotalStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICustomerLookupClient, CreditCustomerClient>();
builder.Services.AddScoped<LendingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<RepaymentQueryService>();
builder.Services.AddScoped<LitigationService>();
builder.Services.AddScoped<ActiveLoanTotalService>();
builder.Services.AddHttpClient("Access", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Access:BaseUrl"]
                                 ?? throw new InvalidOperationException("Access:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddScoped<AccessActorClient>();
builder.Services.AddHttpClient("Credit", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Credit:BaseUrl"]
                                 ?? throw new InvalidOperationException("Credit:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

var lendingConnectionString = app.Configuration.GetConnectionString("Lending")
                              ?? throw new InvalidOperationException("ConnectionStrings:Lending is required.");
foreach (var name in await MigrationRunner.ApplyAsync(lendingConnectionString, typeof(Program).Assembly,
             "Lending.Api.Migrations."))
    app.Logger.LogInformation("Applied database migration {Migration}", name);

app.MapRoutes();

app.Run();

public partial class Program;