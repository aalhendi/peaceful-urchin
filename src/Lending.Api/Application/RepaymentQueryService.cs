using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record CustomerRepaymentSummary(
    IReadOnlyList<DelinquentLoan> DelinquentLoans,
    DuePayment? NextDuePayment,
    IReadOnlyList<RecordedPayment> LastFivePayments);

internal sealed record RepaymentSummaryFound(CustomerRepaymentSummary Summary);

internal union RepaymentSummaryOutcome(RepaymentSummaryFound, LoanForbidden);

internal interface IRepaymentQueryStore
{
    Task<CustomerRepaymentSummary> ReadAsync(CivilId customerId, LendingActor actor, DateOnly today);
}

internal sealed class RepaymentQueryService(IRepaymentQueryStore store)
{
    public async Task<RepaymentSummaryOutcome> ReadAsync(LendingActor actor, CivilId customerId, DateOnly today)
    {
        if (!actor.MayReadCustomerLoans) return new LoanForbidden();
        return new RepaymentSummaryFound(await store.ReadAsync(customerId, actor, today));
    }
}