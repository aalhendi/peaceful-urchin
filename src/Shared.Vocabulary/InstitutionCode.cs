namespace Shared.Vocabulary;

public sealed record InstitutionCode
{
    private InstitutionCode(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<InstitutionCode> Parse(string? value)
    {
        if (value is not { Length: >= 1 and <= 32 })
            return new ParseError("Institution code must be 1 to 32 characters.");

        foreach (var character in value)
            if (!char.IsAsciiLetterUpper(character) &&
                !char.IsAsciiDigit(character) && character != '_')
                return new ParseError("Institution code may contain only A-Z, 0-9, and _.");

        return new InstitutionCode(value);
    }
}