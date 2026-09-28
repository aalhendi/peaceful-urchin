using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record PaymentId
{
    private PaymentId(Guid value) => Value = value;

    public Guid Value { get; }

    public static PaymentId New() => new(Guid.CreateVersion7());

    public static ParseResult<PaymentId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Payment ID cannot be empty.") : new PaymentId(value);
}

internal sealed record PaymentReference
{
    private PaymentReference(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<PaymentReference> Parse(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(character => !char.IsControl(character)) && value == value.Trim()
            ? new PaymentReference(value)
            : new ParseError("Payment reference must be 1 to 100 non-control characters without surrounding spaces.");
}

internal sealed record PaymentDraft(
    LoanId LoanId,
    DateOnly PaymentDate,
    PositiveKwdAmount Amount,
    PaymentReference Reference);

internal sealed record LoanPayment(
    PaymentId Id,
    LoanId LoanId,
    DateOnly PaymentDate,
    PositiveKwdAmount Amount,
    PaymentReference Reference);

internal sealed record DelinquentLoan(LoanId LoanId, PositiveKwdAmount OverdueAmount);

internal sealed record DuePayment(LoanId LoanId, DateOnly DueDate, PositiveKwdAmount RemainingAmount, bool Overdue);

internal sealed record RecordedPayment(
    PaymentId Id,
    LoanId LoanId,
    DateOnly PaymentDate,
    PositiveKwdAmount Amount,
    PaymentReference Reference);