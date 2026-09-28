using Shared.Vocabulary;

namespace Access.Api.Domain;

internal sealed record StaffUserId
{
    private StaffUserId(Guid value) => Value = value;

    public Guid Value { get; }

    public static StaffUserId New() => new(Guid.CreateVersion7());

    public static ParseResult<StaffUserId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Staff user ID cannot be empty.") : new StaffUserId(value);
}

internal sealed record StaffUserName
{
    private StaffUserName(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<StaffUserName> Parse(string? value)
    {
        if (value is not { Length: >= 3 and <= 100 })
            return new ParseError("Username must be 3 to 100 characters.");

        foreach (var character in value)
            if (char.IsWhiteSpace(character) || char.IsControl(character))
                return new ParseError("Username cannot contain whitespace or control characters.");

        return new StaffUserName(value.ToLowerInvariant());
    }
}

internal sealed record StaffUser(
    StaffUserId Id,
    StaffUserName UserName,
    InstitutionId InstitutionId,
    InstitutionKind InstitutionKind,
    bool Active);