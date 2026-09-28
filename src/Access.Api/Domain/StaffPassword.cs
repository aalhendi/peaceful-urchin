using Shared.Vocabulary;

namespace Access.Api.Domain;

internal sealed record StaffPassword : SensitiveString
{
    private StaffPassword(string value) : base(value)
    {
    }

    public static ParseResult<StaffPassword> Parse(string? value) =>
        value is { Length: > 0 and <= 1024 }
            ? new StaffPassword(value)
            : new ParseError("Invalid password.");
}
