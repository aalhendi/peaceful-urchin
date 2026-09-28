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
        var ids = new List<PaymentId>(payments.Count);

        foreach (var payment in payments)
        {
            var loan = await connection.QuerySingleOrDefaultAsync<LoanRow>("""
                                                                           SELECT institution_id AS "InstitutionId",
                                                                                  start_date AS "StartDate"
                                                                           FROM loans WHERE id = @LoanId
                                                                           """, new { LoanId = payment.LoanId.Value },
                transaction);
            if (loan is null || loan.InstitutionId != institutionId.Value)
                return new PaymentLoanMissing();
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

        await transaction.CommitAsync();
        return new PaymentsRecorded(ids);
    }

    private static PaymentId ReadPaymentId(Guid value) => PaymentId.Parse(value).Value is PaymentId id
        ? id
        : throw new InvalidOperationException("Invalid payment ID in the repayment database.");

    private sealed record LoanRow(Guid InstitutionId, DateOnly StartDate);

    private sealed record ExistingPayment(Guid Id, Guid LoanId, DateOnly PaymentDate, decimal AmountKwd);
}