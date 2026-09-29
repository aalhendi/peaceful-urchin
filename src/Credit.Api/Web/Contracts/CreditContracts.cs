namespace Credit.Api.Web.Contracts;

internal sealed class CustomerIdRequest
{
    public string? CustomerCivilId { get; init; }
}

internal sealed class UpdateCustomerNameRequest
{
    public string? CustomerCivilId { get; init; }
    public string? Name { get; init; }
}

internal sealed record CustomerProfileResponse(string CustomerCivilId, string Name, DateOnly DateOfBirth);