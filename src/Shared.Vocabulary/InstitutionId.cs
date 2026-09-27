namespace Shared.Vocabulary;

public sealed record InstitutionId
{
    private InstitutionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static InstitutionId New() => new(Guid.CreateVersion7());

    public static ParseResult<InstitutionId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Institution ID cannot be empty.") : new InstitutionId(value);
}