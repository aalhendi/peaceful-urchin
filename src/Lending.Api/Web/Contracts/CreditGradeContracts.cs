using System.Text.Json.Serialization;

namespace Lending.Api.Web.Contracts;

internal sealed class CreditGradeRequest
{
    public string? CustomerCivilId { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<CreditGradeValue>))]
internal enum CreditGradeValue
{
    A,
    B,
    C,
    F,
    PendingReview
}

internal sealed record CreditGradeResponse(CreditGradeValue Grade, DateOnly AsOfDate);