using System.Data;
using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class CreditGradeStore(NpgsqlDataSource dataSource) : ICreditGradeStore
{
    public async Task<IReadOnlyList<GradeLoan>> ReadAsync(CivilId customerId, DateOnly today)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var parameters = new { CustomerId = customerId.ExposeSecret(), Today = today };
        var loans = (await connection.QueryAsync<LoanRow>("""
                                                          SELECT loans.id AS "Id", loans.start_date AS "StartDate",
                                                                 loans.status AS "Status",
                                                                 loans.amount_kwd AS "PrincipalKwd",
                                                                 COALESCE((SELECT sum(amount_kwd) FROM loan_installments
                                                                           WHERE loan_id = loans.id AND due_date < @Today), 0)
                                                                 - COALESCE((SELECT sum(amount_kwd) FROM loan_payments
                                                                             WHERE loan_id = loans.id AND payment_date <= @Today), 0)
                                                                   AS "OverdueAmountKwd"
                                                          FROM loans
                                                          WHERE loans.customer_civil_id = @CustomerId
                                                          """, parameters, transaction)).ToArray();
        var cases = (await connection.QueryAsync<CaseRow>("""
                                                          SELECT litigation.loan_id AS "LoanId",
                                                                 litigation.status AS "Status",
                                                                 litigation.verdict_date AS "VerdictDate"
                                                          FROM loan_litigations AS litigation
                                                          JOIN loans ON loans.id = litigation.loan_id
                                                          WHERE loans.customer_civil_id = @CustomerId
                                                          """, parameters, transaction)).ToArray();
        await transaction.CommitAsync();

        var casesByLoan = cases.GroupBy(row => row.LoanId)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<LitigationState>)group.Select(ReadState).ToArray());
        return loans.Select(row =>
        {
            var id = LoanId.Parse(row.Id).Value as LoanId
                     ?? throw new InvalidOperationException("Invalid loan ID in the lending database.");
            var status = LoanStatus.Parse(row.Status).Value as LoanStatus
                         ?? throw new InvalidOperationException("Invalid loan status in the lending database.");
            var principal = LoanPrincipal.Parse(row.PrincipalKwd).Value as LoanPrincipal
                            ?? throw new InvalidOperationException("Invalid loan principal in the lending database.");
            var overdue = KwdAmount.Parse(row.OverdueAmountKwd).Value as KwdAmount
                          ?? throw new InvalidOperationException("Invalid overdue amount in the lending database.");
            return new GradeLoan(id, row.StartDate, status, principal, overdue,
                casesByLoan.GetValueOrDefault(id.Value) ?? []);
        }).ToArray();
    }

    private static LitigationState ReadState(CaseRow row) =>
        LitigationStates.Parse(row.Status, row.VerdictDate).Value is LitigationState state
            ? state
            : throw new InvalidOperationException("Invalid litigation state in the lending database.");

    private sealed record LoanRow(
        Guid Id,
        DateOnly StartDate,
        string Status,
        decimal PrincipalKwd,
        decimal OverdueAmountKwd);

    private sealed record CaseRow(Guid LoanId, string Status, DateOnly? VerdictDate);
}