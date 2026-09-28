using Credit.Api.Application;
using Credit.Api.Domain;
using Dapper;
using Npgsql;
using Shared.Vocabulary;

namespace Credit.Api.Infrastructure;

internal sealed class CustomerStore(NpgsqlDataSource dataSource) : ICustomerStore
{
    public async Task<Customer?> FindAsync(CivilId customerId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<CustomerRow>("""
                                                                          SELECT civil_id AS "CivilId", loan_eligible AS "LoanEligible"
                                                                          FROM customers WHERE civil_id = @CustomerId
                                                                          """,
            new { CustomerId = customerId.ExposeSecret() });
        if (row is null) return null;
        var id = CivilId.Parse(row.CivilId).Value as CivilId
                 ?? throw new InvalidOperationException("Stored customer Civil ID is invalid.");
        return new Customer(id, row.LoanEligible);
    }

    private sealed record CustomerRow(string CivilId, bool LoanEligible);
}