using Shared.Vocabulary;

namespace Credit.Api.Domain;

internal sealed record CustomerName
{
    private CustomerName(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<CustomerName> Parse(string? value)
    {
        var name = value?.Trim();
        // TODO(aalhendi): Language? Count unicode codepoints? Or limited to English? Arbitrary limit
        if (name is not { Length: >= 1 and <= 200 })
            return new ParseError("Customer name must be 1 to 200 characters.");

        foreach (var character in name)
            if (char.IsControl(character))
                return new ParseError("Customer name cannot contain control characters.");

        return new CustomerName(name);
    }
}