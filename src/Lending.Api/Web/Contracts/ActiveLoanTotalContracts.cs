namespace Lending.Api.Web.Contracts;

internal sealed class ActiveLoanTotalRequest
{
    public string? CustomerCivilId { get; init; }
}

internal sealed record ActiveLoanTotalResponse(long LoanCount, decimal OriginalPrincipalTotalKwd);