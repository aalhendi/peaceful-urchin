using System.Data;
using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class RepaymentQueryStore(NpgsqlDataSource dataSource) : IRepaymentQueryStore
{
    public async Task<CustomerRepaymentSummary> ReadAsync(CivilId customerId, LendingActor actor, DateOnly today)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var parameters = new
        {
            CustomerId = customerId.ExposeSecret(),
            InstitutionId = actor.InstitutionId.Value,
            IsCinet = actor.InstitutionKind == InstitutionKind.Cinet,
            Today = today
        };

        var delinquentRows = await connection.QueryAsync<DelinquentRow>(DelinquentSql, parameters, transaction);
        var dueRow = await connection.QuerySingleOrDefaultAsync<DueRow>(NextDueSql, parameters, transaction);
        var paymentRows = await connection.QueryAsync<PaymentRow>(RecentPaymentsSql, parameters, transaction);

        await transaction.CommitAsync();
        return new CustomerRepaymentSummary(
            delinquentRows.Select(row => new DelinquentLoan(
                ReadLoanId(row.LoanId), ReadAmount(row.OverdueAmountKwd))).ToArray(),
            dueRow is null
                ? null
                : new DuePayment(
                    ReadLoanId(dueRow.LoanId), dueRow.DueDate,
                    ReadAmount(dueRow.RemainingAmountKwd), dueRow.DueDate < today),
            paymentRows.Select(row => new RecordedPayment(
                ReadPaymentId(row.Id), ReadLoanId(row.LoanId), row.PaymentDate,
                ReadAmount(row.AmountKwd), ReadReference(row.Reference))).ToArray());
    }

    private static LoanId ReadLoanId(Guid value) => LoanId.Parse(value).Value is LoanId id
        ? id
        : throw new InvalidOperationException("Invalid loan ID in the repayment database.");

    private static PaymentId ReadPaymentId(Guid value) => PaymentId.Parse(value).Value is PaymentId id
        ? id
        : throw new InvalidOperationException("Invalid payment ID in the repayment database.");

    private static PositiveKwdAmount ReadAmount(decimal value) =>
        PositiveKwdAmount.Parse(value).Value is PositiveKwdAmount amount
            ? amount
            : throw new InvalidOperationException("Invalid KWD amount in the repayment database.");

    private static PaymentReference ReadReference(string value) =>
        PaymentReference.Parse(value).Value is PaymentReference reference
            ? reference
            : throw new InvalidOperationException("Invalid payment reference in the repayment database.");

    private sealed record DelinquentRow(Guid LoanId, decimal OverdueAmountKwd);

    private sealed record DueRow(Guid LoanId, DateOnly DueDate, decimal RemainingAmountKwd);

    private sealed record PaymentRow(Guid Id, Guid LoanId, DateOnly PaymentDate, decimal AmountKwd, string Reference);

    // NOTE(aalhendi): An unpaid installment becomes overdue the day after its due date.
    // NOTE(aalhendi): postgres SUM() returns NULL if there are no rows to sum... COALESCE to 0 if thats the case. 
    private const string DelinquentSql = """
                                         WITH amounts AS (
                                             SELECT loans.id AS "LoanId",
                                                    COALESCE((SELECT sum(amount_kwd) FROM loan_installments
                                                              WHERE loan_id = loans.id AND due_date < @Today), 0)
                                                    - COALESCE((SELECT sum(amount_kwd) FROM loan_payments
                                                                WHERE loan_id = loans.id AND payment_date <= @Today), 0)
                                                      AS "OverdueAmountKwd"
                                             FROM loans
                                             WHERE customer_civil_id = @CustomerId AND status = 'Open'
                                               AND (@IsCinet OR institution_id = @InstitutionId)
                                         )
                                         SELECT "LoanId", "OverdueAmountKwd" FROM amounts
                                         WHERE "OverdueAmountKwd" > 0 ORDER BY "LoanId"
                                         """;

    private const string NextDueSql = """
                                      WITH visible_loans AS (
                                          SELECT id FROM loans
                                          WHERE customer_civil_id = @CustomerId AND status = 'Open'
                                            AND (@IsCinet OR institution_id = @InstitutionId)
                                      ), payment_totals AS (
                                          SELECT loan_payments.loan_id, sum(loan_payments.amount_kwd) AS total_paid
                                          FROM loan_payments
                                          JOIN visible_loans ON visible_loans.id = loan_payments.loan_id
                                          WHERE loan_payments.payment_date <= @Today
                                          GROUP BY loan_payments.loan_id
                                      ), installments AS (
                                          SELECT loan_installments.loan_id, loan_installments.due_date,
                                                 loan_installments.amount_kwd,
                                                 sum(loan_installments.amount_kwd) OVER (
                                                     PARTITION BY loan_installments.loan_id
                                                     ORDER BY loan_installments.sequence_no
                                                 ) AS scheduled_through_installment,
                                                 COALESCE(payment_totals.total_paid, 0) AS total_paid
                                          FROM visible_loans
                                          JOIN loan_installments ON loan_installments.loan_id = visible_loans.id
                                          LEFT JOIN payment_totals ON payment_totals.loan_id = visible_loans.id
                                      )
                                      SELECT loan_id AS "LoanId", due_date AS "DueDate",
                                             LEAST(amount_kwd, scheduled_through_installment - total_paid)
                                                 AS "RemainingAmountKwd"
                                      FROM installments
                                      WHERE scheduled_through_installment > total_paid
                                      ORDER BY due_date, loan_id LIMIT 1
                                      """;

    private const string RecentPaymentsSql = """
                                             SELECT loan_payments.id AS "Id", loan_payments.loan_id AS "LoanId",
                                                    loan_payments.payment_date AS "PaymentDate",
                                                    loan_payments.amount_kwd AS "AmountKwd",
                                                    loan_payments.external_reference AS "Reference"
                                             FROM loan_payments
                                             JOIN loans ON loans.id = loan_payments.loan_id
                                             WHERE loans.customer_civil_id = @CustomerId
                                               AND (@IsCinet OR loans.institution_id = @InstitutionId)
                                               AND loan_payments.payment_date <= @Today
                                             ORDER BY loan_payments.payment_date DESC, loan_payments.recorded_at DESC,
                                                      loan_payments.id DESC
                                             LIMIT 5
                                             """;
}