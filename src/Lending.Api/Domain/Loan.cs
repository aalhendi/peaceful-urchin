using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record LoanId
{
    private LoanId(Guid value) => Value = value;

    public Guid Value { get; }

    public static LoanId New() => new(Guid.CreateVersion7());
}

internal sealed record LoanTenor
{
    private LoanTenor(int months) => Months = months;

    public int Months { get; }

    public static ParseResult<LoanTenor> Parse(int months) =>
        months > 0 ? new LoanTenor(months) : new ParseError("Loan tenor must be positive.");
}

internal sealed record LoanPrincipal
{
    private LoanPrincipal(KwdAmount amount) => Amount = amount;

    public KwdAmount Amount { get; }

    public static ParseResult<LoanPrincipal> Parse(decimal dinars) => KwdAmount.Parse(dinars) switch
    {
        KwdAmount { Dinars: > 0 } amount => new LoanPrincipal(amount),
        KwdAmount => new ParseError("Loan amount must be positive."),
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
    LoanStatus Status);