using Credit.Api.Domain;
using Shared.Vocabulary;

namespace Credit.Api.Application;

internal sealed record CreditActor(InstitutionKind Kind, IReadOnlySet<string> Permissions)
{
    public bool MayCheckLoanEligibility =>
        Kind == InstitutionKind.Bank && Permissions.Contains("Loan.Create") ||
        Kind == InstitutionKind.Cinet && Permissions.Contains("Loan.Block");
}

internal sealed record EligibilityKnown(bool Eligible);

internal sealed record CustomerMissing;

internal sealed record EligibilityForbidden;

internal union EligibilityOutcome(EligibilityKnown, CustomerMissing, EligibilityForbidden);

internal sealed class CustomerService(ICustomerStore store)
{
    public async Task<EligibilityOutcome> CheckLoanEligibilityAsync(CreditActor actor, CivilId customerId)
    {
        if (!actor.MayCheckLoanEligibility) return new EligibilityForbidden();
        var customer = await store.FindAsync(customerId);
        return customer is null ? new CustomerMissing() : new EligibilityKnown(customer.LoanEligible);
    }
}