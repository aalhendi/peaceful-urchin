namespace Shared.Vocabulary;

public sealed record CivilId : SensitiveString
{
    private CivilId(string value, DateOnly dateOfBirth) : base(value)
    {
        DateOfBirth = dateOfBirth;
    }

    public DateOnly DateOfBirth { get; }

    public static ParseResult<CivilId> Parse(string? value)
    {
        if (value is null) return new ParseError("Civil ID is required.");
        if (value.Length != 12) return new ParseError("Civil ID must have 12 digits.");

        foreach (var digit in value)
            if (!char.IsAsciiDigit(digit))
                return new ParseError("Civil ID must contain only ASCII digits.");

        if (ReadBirthDate(value) is not DateOnly dateOfBirth)
            return new ParseError("Civil ID contains an invalid birth date.");
        if (!HasValidChecksum(value)) return new ParseError("Civil ID has an invalid checksum.");

        return new CivilId(value, dateOfBirth);
    }

    private static DateOnly? ReadBirthDate(string value)
    {
        var century = value[0] switch
        {
            '1' => 1800,
            '2' => 1900,
            '3' => 2000,
            _ => 0
        };
        if (century == 0) return null;

        var year = century + (value[1] - '0') * 10 + (value[2] - '0');
        var month = (value[3] - '0') * 10 + (value[4] - '0');
        var day = (value[5] - '0') * 10 + (value[6] - '0');

        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    private static bool HasValidChecksum(string value)
    {
        ReadOnlySpan<int> weights = [2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
        var sum = 0;
        for (var index = 0; index < weights.Length; index++)
            sum += (value[index] - '0') * weights[index];

        return 11 - sum % 11 == value[11] - '0';
    }
}