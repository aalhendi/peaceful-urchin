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
                                                                          SELECT civil_id AS "CivilId", name AS "Name"
                                                                          FROM customers WHERE civil_id = @CustomerId
                                                                          """,
            new { CustomerId = customerId.ExposeSecret() });
        if (row is null) return null;
        var id = CivilId.Parse(row.CivilId).Value as CivilId
                 ?? throw new InvalidOperationException("Stored customer Civil ID is invalid.");
        var name = CustomerName.Parse(row.Name).Value as CustomerName
                   ?? throw new InvalidOperationException("Stored customer name is invalid.");
        return new Customer(id, name);
    }

    public async Task<bool> UpdateNameAsync(CivilId customerId, CustomerName name)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var updated = await connection.ExecuteAsync("""
                                                    UPDATE customers SET name = @Name
                                                    WHERE civil_id = @CustomerId
                                                    """,
            new { CustomerId = customerId.ExposeSecret(), Name = name.Value });
        return updated == 1;
    }

    private sealed record CustomerRow(string CivilId, string Name);
}