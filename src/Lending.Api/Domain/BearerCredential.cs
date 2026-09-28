using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record BearerCredential : SensitiveString
{
    private BearerCredential(string value) : base(value)
    {
    }

    public static ParseResult<BearerCredential> Parse(string? value)
    {
        if (value is not { Length: > 0 and <= 512 }) return new ParseError("Invalid bearer credential.");
        foreach (var character in value)
            if (char.IsWhiteSpace(character) || char.IsControl(character))
                return new ParseError("Invalid bearer credential.");

        return new BearerCredential(value);
    }
}