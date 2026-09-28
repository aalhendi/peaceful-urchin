using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class LendingStore(NpgsqlDataSource dataSource) : ILendingStore
{
    public async Task<bool> CreateLoanAsync(Loan loan)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await EnsureLoanBlockRowAsync(connection, transaction, loan.CustomerId);

        var blocked = await connection.QuerySingleAsync<bool>("""
                                                              SELECT blocked FROM customer_loan_blocks
                                                              WHERE customer_civil_id = @CustomerId FOR UPDATE
                                                              """, new { CustomerId = loan.CustomerId.ExposeSecret() },
            transaction);
        if (blocked) return false;

        await connection.ExecuteAsync("""
                                      INSERT INTO loans (id, customer_civil_id, institution_id, start_date,
                                                         tenor_months, amount_kwd, rate_percent, status)
                                      VALUES (@Id, @CustomerId, @InstitutionId, @StartDate,
                                              @TenorMonths, @AmountKwd, @RatePercent, @Status)
                                      """, new
        {
            Id = loan.Id.Value,
            CustomerId = loan.CustomerId.ExposeSecret(),
            InstitutionId = loan.InstitutionId.Value,
            StartDate = loan.StartDate,
            TenorMonths = loan.Tenor.Months,
            AmountKwd = loan.Principal.Amount.Dinars,
            RatePercent = loan.Rate.PercentPoints,
            Status = loan.Status.Value
        }, transaction);
        await transaction.CommitAsync();
        return true;
    }

    public async Task SetLoanBlockAsync(CivilId customerId, bool blocked, StaffActorId changedBy)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await EnsureLoanBlockRowAsync(connection, transaction, customerId);
        var changed = await connection.ExecuteAsync("""
                                                    UPDATE customer_loan_blocks SET blocked = @Blocked
                                                    WHERE customer_civil_id = @CustomerId AND blocked <> @Blocked
                                                    """,
            new { Blocked = blocked, CustomerId = customerId.ExposeSecret() },
            transaction);
        if (changed == 1)
            await connection.ExecuteAsync("""
                                          INSERT INTO loan_block_changes (id, customer_civil_id, blocked, changed_by)
                                          VALUES (@Id, @CustomerId, @Blocked, @ChangedBy)
                                          """, new
            {
                Id = Guid.CreateVersion7(), CustomerId = customerId.ExposeSecret(),
                Blocked = blocked, ChangedBy = changedBy.Value
            }, transaction);
        await transaction.CommitAsync();
    }

    private static async Task EnsureLoanBlockRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CivilId customerId) =>
        await connection.ExecuteAsync("""
                                      INSERT INTO customer_loan_blocks (customer_civil_id)
                                      VALUES (@CustomerId) ON CONFLICT DO NOTHING
                                      """, new { CustomerId = customerId.ExposeSecret() }, transaction);
}