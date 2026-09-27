namespace Shared.Vocabulary;

public sealed record FinancingRate
{
    private FinancingRate(decimal percentPoints) => PercentPoints = percentPoints;

    public decimal PercentPoints { get; }

    // NOTE(aalhendi): The financing contract supplies the period, calculation, and allowed maximum.
    public static ParseResult<FinancingRate> Parse(decimal percentPoints) =>
        percentPoints < 0
            ? new ParseError("Financing rate cannot be negative.")
            : new FinancingRate(percentPoints);

    public decimal AsFraction() => PercentPoints / 100m;
}