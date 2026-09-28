using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record LoanId
{
    private LoanId(Guid value) => Value = value;

    public Guid Value { get; }

    public static LoanId New() => new(Guid.CreateVersion7());

    public static ParseResult<LoanId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Loan ID cannot be empty.") : new LoanId(value);
}

internal sealed record LoanTenor
{
    private LoanTenor(int months) => Months = months;

    public int Months { get; }

    // NOTE(aalhendi): This is an arbitrary limit. Business usually provides these bounds.
    public static ParseResult<LoanTenor> Parse(int months) =>
        months is > 0 and <= 360 ? new LoanTenor(months) : new ParseError("Loan tenor must be 1 to 360 months.");
}

internal sealed record LoanPrincipal
{
    private LoanPrincipal(PositiveKwdAmount amount) => Amount = amount;

    public PositiveKwdAmount Amount { get; }

    public static ParseResult<LoanPrincipal> Parse(decimal dinars) => PositiveKwdAmount.Parse(dinars) switch
    {
        PositiveKwdAmount amount => new LoanPrincipal(amount),
        ParseError error => error
    };
}

internal sealed record LoanStatus
{
    public static readonly LoanStatus Open = new("Open");
    public static readonly LoanStatus Closed = new("Closed");

    private LoanStatus(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<LoanStatus> Parse(string? value) => value switch
    {
        "Open" => Open,
        "Closed" => Closed,
        _ => new ParseError("Unknown loan status.")
    };
}

internal sealed record Loan(
    LoanId Id,
    CivilId CustomerId,
    InstitutionId InstitutionId,
    DateOnly StartDate,
    LoanTenor Tenor,
    LoanPrincipal Principal,
    FinancingRate Rate,
    LoanStatus Status,
    RepaymentSchedule Schedule);
