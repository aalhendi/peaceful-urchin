using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record ActiveLoanTotal(long LoanCount, KwdAmount OriginalPrincipalTotal);

internal sealed record ActiveLoanTotalFound(ActiveLoanTotal Total);

internal union ActiveLoanTotalOutcome(ActiveLoanTotalFound, LoanForbidden);

internal interface IActiveLoanTotalStore
{
    Task<ActiveLoanTotal> ReadAsync(CivilId customerId, LendingActor actor);
}

internal sealed class ActiveLoanTotalService(IActiveLoanTotalStore store)
{
    public async Task<ActiveLoanTotalOutcome> ReadAsync(LendingActor actor, CivilId customerId)
    {
        if (!actor.MayReadCustomerLoans) return new LoanForbidden();
        return new ActiveLoanTotalFound(await store.ReadAsync(customerId, actor));
    }
}