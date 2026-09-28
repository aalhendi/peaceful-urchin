using Shared.Vocabulary;

namespace Credit.Api.Domain;

internal sealed record Customer(CivilId Id, bool LoanEligible);

internal sealed record InstitutionKind
{
    public static readonly InstitutionKind Bank = new("Bank");
    public static readonly InstitutionKind Cinet = new("CINET");

    private InstitutionKind(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<InstitutionKind> Parse(string? value) => value switch
    {
        "Bank" => Bank,
        "CINET" => Cinet,
        _ => new ParseError("Unknown institution kind.")
    };
}