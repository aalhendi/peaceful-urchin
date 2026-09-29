using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record PaymentsRecorded(IReadOnlyList<PaymentId> Ids);

internal sealed record PaymentLoanMissing;

internal sealed record PaymentReferenceConflict;

internal sealed record PaymentBeforeLoanStart;
internal sealed record PaymentInFuture;

internal union UploadPaymentsOutcome(
    PaymentsRecorded, PaymentLoanMissing, PaymentReferenceConflict, PaymentBeforeLoanStart, PaymentInFuture,
    LoanForbidden);

internal interface IPaymentStore
{
    Task<UploadPaymentsOutcome> RecordAsync(InstitutionId institutionId, IReadOnlyList<LoanPayment> payments);
}

internal sealed class PaymentService(IPaymentStore store)
{
    public async Task<UploadPaymentsOutcome> UploadAsync(
        LendingActor actor, IReadOnlyList<PaymentDraft> drafts, DateOnly today)
    {
        if (!actor.MayUploadPayments) return new LoanForbidden();
        if (drafts.Any(draft => draft.PaymentDate > today)) return new PaymentInFuture();
        var payments = drafts.Select(draft => new LoanPayment(
            PaymentId.New(), draft.LoanId, draft.PaymentDate, draft.Amount, draft.Reference)).ToArray();
        return await store.RecordAsync(actor.InstitutionId, payments);
    }
}