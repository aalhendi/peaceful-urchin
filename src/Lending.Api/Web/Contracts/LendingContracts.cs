namespace Lending.Api.Web.Contracts;

internal sealed class CreateLoanRequest
{
    public string? CustomerCivilId { get; init; }
    public DateOnly? StartDate { get; init; }
    public int? TenorMonths { get; init; }
    public decimal? AmountKwd { get; init; }
    public decimal? RatePercent { get; init; }
}

internal sealed record LoanCreatedResponse(Guid Id);

internal sealed class SetLoanBlockRequest
{
    public string? CustomerCivilId { get; init; }
    public bool? Blocked { get; init; }
}