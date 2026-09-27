namespace Shared.Vocabulary;

public sealed record KwdAmount
{
    private KwdAmount(decimal dinars) => Dinars = dinars;

    public decimal Dinars { get; }

    // NOTE(aalhendi): One dinar has 1,000 fils; calculations may need finer precision before settlement.
    public static ParseResult<KwdAmount> Parse(decimal dinars) =>
        decimal.Round(dinars, 3) == dinars
            ? new KwdAmount(dinars)
            : new ParseError("KWD amount cannot have more than three decimal places.");
}