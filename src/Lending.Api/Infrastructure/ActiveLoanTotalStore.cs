using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class ActiveLoanTotalStore(NpgsqlDataSource dataSource) : IActiveLoanTotalStore
{
    public async Task<ActiveLoanTotal> ReadAsync(CivilId customerId, LendingActor actor)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QuerySingleAsync<TotalRow>("""
                                                              SELECT count(*) AS "LoanCount",
                                                                     COALESCE(sum(loans.amount_kwd), 0) AS "OriginalPrincipalTotalKwd"
                                                              FROM loans
                                                              WHERE loans.customer_civil_id = @CustomerId
                                                                AND loans.status = 'Open'
                                                                AND (@IsCinet OR loans.institution_id = @InstitutionId)
                                                                AND NOT EXISTS (
                                                                    SELECT 1 FROM loan_litigations
                                                                    WHERE loan_id = loans.id AND status = 'Pending'
                                                                )
                                                              """, new
        {
            CustomerId = customerId.ExposeSecret(),
            IsCinet = actor.InstitutionKind == InstitutionKind.Cinet,
            InstitutionId = actor.InstitutionId.Value
        });
        var principalTotal = KwdAmount.Parse(row.OriginalPrincipalTotalKwd).Value as KwdAmount
                             ?? throw new InvalidOperationException("Invalid principal total in the lending database.");
        return new ActiveLoanTotal(row.LoanCount, principalTotal);
    }

    private sealed record TotalRow(long LoanCount, decimal OriginalPrincipalTotalKwd);
}