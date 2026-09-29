using Credit.Api.Domain;
using Shared.Vocabulary;

namespace Credit.Api.Application;

internal sealed record CreditActor(InstitutionKind Kind, IReadOnlySet<string> Permissions)
{
    public bool MayFindCustomer =>
        Permissions.Contains("Customer.Read") ||
        Kind == InstitutionKind.Bank && Permissions.Contains("Loan.Create") ||
        Kind == InstitutionKind.Cinet && Permissions.Contains("Loan.Block");

    public bool MayReadProfile => Permissions.Contains("Customer.Read");
    public bool MayUpdateName => Permissions.Contains("Customer.Write");
}

internal sealed record CustomerFound;

internal sealed record CustomerMissing;

internal sealed record CustomerForbidden;

internal union CustomerLookupOutcome(CustomerFound, CustomerMissing, CustomerForbidden);

internal sealed record CustomerProfileFound(Customer Customer);

internal union CustomerProfileOutcome(CustomerProfileFound, CustomerMissing, CustomerForbidden);

internal sealed record CustomerNameUpdated;

internal union UpdateCustomerNameOutcome(CustomerNameUpdated, CustomerMissing, CustomerForbidden);

internal sealed class CustomerService(ICustomerStore store)
{
    public async Task<CustomerLookupOutcome> FindCustomerAsync(CreditActor actor, CivilId customerId)
    {
        if (!actor.MayFindCustomer) return new CustomerForbidden();
        var customer = await store.FindAsync(customerId);
        return customer is null ? new CustomerMissing() : new CustomerFound();
    }

    public async Task<CustomerProfileOutcome> ReadProfileAsync(CreditActor actor, CivilId customerId)
    {
        if (!actor.MayReadProfile) return new CustomerForbidden();
        var customer = await store.FindAsync(customerId);
        return customer is null ? new CustomerMissing() : new CustomerProfileFound(customer);
    }

    public async Task<UpdateCustomerNameOutcome> UpdateNameAsync(CreditActor actor, CivilId customerId,
        CustomerName name)
    {
        if (!actor.MayUpdateName) return new CustomerForbidden();
        return await store.UpdateNameAsync(customerId, name) ? new CustomerNameUpdated() : new CustomerMissing();
    }
}