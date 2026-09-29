using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record LitigationId
{
    private LitigationId(Guid value) => Value = value;

    public Guid Value { get; }

    public static LitigationId New() => new(Guid.CreateVersion7());

    public static ParseResult<LitigationId> Parse(Guid value) =>
        value == Guid.Empty ? new ParseError("Litigation ID cannot be empty.") : new LitigationId(value);
}

internal sealed record CourtId
{
    private CourtId(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<CourtId> Parse(string? value)
    {
        var courtId = value?.Trim();
        if (courtId is not { Length: >= 1 and <= 100 })
            return new ParseError("Court ID must be 1 to 100 characters.");

        foreach (var character in courtId)
            if (char.IsControl(character))
                return new ParseError("Court ID cannot contain control characters.");

        return new CourtId(courtId);
    }
}

internal sealed record Pending;

internal sealed record Guilty(DateOnly VerdictDate);

internal sealed record Innocent(DateOnly VerdictDate);

internal union LitigationState(Pending, Guilty, Innocent);

internal union Verdict(Guilty, Innocent);

internal static class LitigationStates
{
    public static ParseResult<LitigationState> Parse(string? status, DateOnly? date) => (status, date) switch
    {
        ("Pending", null) => (LitigationState)new Pending(),
        ("Guilty", DateOnly verdictDate) => (LitigationState)new Guilty(verdictDate),
        ("Innocent", DateOnly verdictDate) => (LitigationState)new Innocent(verdictDate),
        _ => new ParseError("Litigation status and verdict date disagree.")
    };

    public static ParseResult<Verdict> ParseVerdict(string? status, DateOnly? date, DateOnly today)
    {
        if (status is not ("Guilty" or "Innocent"))
            return new ParseError("Verdict must be Innocent or Guilty.");
        if (date is not DateOnly verdictDate || verdictDate > today)
            return new ParseError("Verdict date is required and cannot be in the future.");

        Verdict verdict = status == "Guilty" ? new Guilty(verdictDate) : new Innocent(verdictDate);
        return verdict;
    }
}

internal sealed record LitigationCase(LitigationId Id, CourtId CourtId, LitigationState State);

internal sealed record LoanLitigation(
    LoanId LoanId,
    CivilId CustomerId,
    InstitutionId InstitutionId,
    IReadOnlyList<LitigationCase> Cases);
