namespace Lending.Api.Web.Contracts;

internal sealed class CreateLoanRequest
{
    public string? CustomerCivilId { get; init; }
    public DateOnly? StartDate { get; init; }
    public int? TenorMonths { get; init; }
    public decimal? AmountKwd { get; init; }
    public decimal? RatePercent { get; init; }
    public InstallmentRequest?[]? Installments { get; init; }
}

// TODO(aalhendi): Realistically, many customers reschedule loans. We don't have a mechanism to do that yet!!
internal sealed class InstallmentRequest
{
    public DateOnly? DueDate { get; init; }
    public decimal? AmountKwd { get; init; }
}

internal sealed record LoanCreatedResponse(Guid Id);

internal sealed class SetLoanBlockRequest
{
    public string? CustomerCivilId { get; init; }
    public bool? Blocked { get; init; }
}

internal sealed class UploadPaymentsRequest
{
    public PaymentRequest?[]? Payments { get; init; }
}

internal sealed class PaymentRequest
{
    public Guid? LoanId { get; init; }
    public DateOnly? PaymentDate { get; init; }
    public decimal? AmountKwd { get; init; }
    public string? ExternalReference { get; init; }
}

internal sealed record UploadPaymentsResponse(IReadOnlyList<Guid> Ids);

internal sealed class RepaymentSummaryRequest
{
    public string? CustomerCivilId { get; init; }
}

internal sealed record DelinquentLoanResponse(Guid LoanId, decimal OverdueAmountKwd);

internal sealed record DuePaymentResponse(Guid LoanId, DateOnly DueDate, decimal RemainingAmountKwd, bool Overdue);

internal sealed record RecordedPaymentResponse(
    Guid Id,
    Guid LoanId,
    DateOnly PaymentDate,
    decimal AmountKwd,
    string ExternalReference);

internal sealed record RepaymentSummaryResponse(
    IReadOnlyList<DelinquentLoanResponse> DelinquentLoans,
    DuePaymentResponse? NextDuePayment,
    IReadOnlyList<RecordedPaymentResponse> LastFivePayments);