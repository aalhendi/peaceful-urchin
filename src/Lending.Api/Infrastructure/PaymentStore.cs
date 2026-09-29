using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class PaymentStore(NpgsqlDataSource dataSource) : IPaymentStore
{
    public async Task<UploadPaymentsOutcome> RecordAsync(
        InstitutionId institutionId, IReadOnlyList<LoanPayment> payments)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var loanIds = payments.Select(payment => payment.LoanId.Value).Distinct().ToArray();
        // NOTE(aalhendi): Lock touched loans in ID order so concurrent uploads see prior payments before deciding closure.
        var loans = (await connection.QueryAsync<LoanRow>("""
                                                         SELECT id AS "Id", start_date AS "StartDate"
                                                         FROM loans
                                                         WHERE id = ANY(@LoanIds) AND institution_id = @InstitutionId
                                                         ORDER BY id FOR UPDATE
                                                         """, new { LoanIds = loanIds, InstitutionId = institutionId.Value },
            transaction)).ToDictionary(loan => loan.Id);
        if (loans.Count != loanIds.Length) return new PaymentLoanMissing();

        var ids = new List<PaymentId>(payments.Count);

        foreach (var payment in payments)
        {
            var loan = loans[payment.LoanId.Value];
            if (payment.PaymentDate < loan.StartDate)
                return new PaymentBeforeLoanStart();

            var insertedId = await connection.QuerySingleOrDefaultAsync<Guid?>("""
                                                                               INSERT INTO loan_payments
                                                                                   (id, loan_id, institution_id, external_reference,
                                                                                    payment_date, amount_kwd)
                                                                               VALUES (@Id, @LoanId, @InstitutionId, @Reference,
                                                                                       @PaymentDate, @AmountKwd)
                                                                               ON CONFLICT (institution_id, external_reference) DO NOTHING
                                                                               RETURNING id
                                                                               """, new
            {
                Id = payment.Id.Value,
                LoanId = payment.LoanId.Value,
                InstitutionId = institutionId.Value,
                Reference = payment.Reference.Value,
                payment.PaymentDate,
                AmountKwd = payment.Amount.Dinars
            }, transaction);
            if (insertedId is Guid id)
            {
                ids.Add(ReadPaymentId(id));
                continue;
            }

            var existing = await connection.QuerySingleAsync<ExistingPayment>("""
                                                                              SELECT id AS "Id", loan_id AS "LoanId",
                                                                                     payment_date AS "PaymentDate",
                                                                                     amount_kwd AS "AmountKwd"
                                                                              FROM loan_payments
                                                                              WHERE institution_id = @InstitutionId
                                                                                AND external_reference = @Reference
                                                                              """, new
            {
                InstitutionId = institutionId.Value,
                Reference = payment.Reference.Value
            }, transaction);
            if (existing.LoanId != payment.LoanId.Value || existing.PaymentDate != payment.PaymentDate ||
                existing.AmountKwd != payment.Amount.Dinars)
                return new PaymentReferenceConflict();
            ids.Add(ReadPaymentId(existing.Id));
        }

        await connection.ExecuteAsync("""
                                      UPDATE loans SET status = 'Closed'
                                      WHERE id = ANY(@LoanIds) AND status = 'Open'
                                        AND (SELECT sum(amount_kwd) FROM loan_payments
                                             WHERE loan_id = loans.id) >=
                                            (SELECT sum(amount_kwd) FROM loan_installments
                                             WHERE loan_id = loans.id)
                                      """, new { LoanIds = loanIds }, transaction);
        await transaction.CommitAsync();
        return new PaymentsRecorded(ids);
    }

    private static PaymentId ReadPaymentId(Guid value) => PaymentId.Parse(value).Value is PaymentId id
        ? id
        : throw new InvalidOperationException("Invalid payment ID in the repayment database.");

    private sealed record LoanRow(Guid Id, DateOnly StartDate);

    private sealed record ExistingPayment(Guid Id, Guid LoanId, DateOnly PaymentDate, decimal AmountKwd);
}
