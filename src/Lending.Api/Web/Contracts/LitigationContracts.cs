namespace Lending.Api.Web.Contracts;

internal sealed class OpenLitigationRequest
{
    public string? CourtId { get; init; }
}

internal sealed record LitigationOpenedResponse(Guid LitigationId);

internal sealed class RecordVerdictRequest
{
    public string? Status { get; init; }
    public DateOnly? VerdictDate { get; init; }
}

internal sealed record LitigationCaseResponse(
    Guid LitigationId,
    string CourtId,
    string Status,
    DateOnly? VerdictDate);

internal sealed record LoanLitigationResponse(
    Guid LoanId,
    string CustomerCivilId,
    Guid InstitutionId,
    IReadOnlyList<LitigationCaseResponse> Cases);