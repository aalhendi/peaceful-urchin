using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Vocabulary;

namespace Access.Api.Domain;

internal sealed record StaffSessionToken : SensitiveString
{
    private StaffSessionToken(string value) : base(value)
    {
    }

    public static StaffSessionToken New() => new(WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)));

    public static ParseResult<StaffSessionToken> Parse(string? value)
    {
        if (value is not { Length: 43 }) return new ParseError("Invalid bearer token.");
        foreach (var character in value)
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
                return new ParseError("Invalid bearer token.");

        return new StaffSessionToken(value);
    }

    public string Digest() => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(ExposeSecret())));
}