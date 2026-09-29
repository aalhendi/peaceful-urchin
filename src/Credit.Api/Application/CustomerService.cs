using Credit.Api.Domain;
using Shared.Vocabulary;

namespace Credit.Api.Application;

internal sealed record CreditActor(InstitutionKind Kind, IReadOnlySet<string> Permissions)
{
    public bool MayFindCustomer =>
        Permissions.Contains("Customer.Read") ||
        Kind == InstitutionKind.Bank && Permissions.Contains("Loan.Create") ||
        Kind == InstitutionKind.Cinet && Permissions.Contains("Loan.Block");
}

internal sealed record CustomerFound;

internal sealed record CustomerMissing;

internal sealed record CustomerLookupForbidden;

internal union CustomerLookupOutcome(CustomerFound, CustomerMissing, CustomerLookupForbidden);

internal sealed class CustomerService(ICustomerStore store)
{
    public async Task<CustomerLookupOutcome> FindCustomerAsync(CreditActor actor, CivilId customerId)
    {
        if (!actor.MayFindCustomer) return new CustomerLookupForbidden();
        var customer = await store.FindAsync(customerId);
        return customer is null ? new CustomerMissing() : new CustomerFound();
    }
}