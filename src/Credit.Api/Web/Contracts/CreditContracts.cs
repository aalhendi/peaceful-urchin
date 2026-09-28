namespace Credit.Api.Web.Contracts;

internal sealed class EligibilityRequest
{
    public string? CustomerCivilId { get; init; }
}

internal sealed record EligibilityResponse(bool Eligible);