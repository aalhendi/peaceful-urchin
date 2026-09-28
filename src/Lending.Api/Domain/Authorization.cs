using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record StaffActorId
{
    private StaffActorId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ParseResult<StaffActorId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Staff actor ID cannot be empty.") : new StaffActorId(value);
}

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