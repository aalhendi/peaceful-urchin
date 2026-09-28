namespace Shared.Vocabulary;

public sealed record PositiveKwdAmount
{
    private PositiveKwdAmount(KwdAmount amount) => Dinars = amount.Dinars;

    public decimal Dinars { get; }

    public static ParseResult<PositiveKwdAmount> Parse(decimal dinars) => KwdAmount.Parse(dinars) switch
    {
        KwdAmount { Dinars: > 0 } amount => new PositiveKwdAmount(amount),
        KwdAmount => new ParseError("Amount must be positive."),
        ParseError error => error
    };
}
