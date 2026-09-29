using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record CreditGradeKnown(CreditGradeDecision Decision);

internal union CreditGradeOutcome(
    CreditGradeKnown, CustomerNotFound, LoanUnauthorized, LoanForbidden, LoanDependencyUnavailable);

internal interface ICreditGradeStore
{
    Task<IReadOnlyList<GradeLoan>> ReadAsync(CivilId customerId, DateOnly today);
}

internal sealed class CreditGradeService(ICreditGradeStore store, ICustomerLookupClient customers)
{
    public async Task<CreditGradeOutcome> ReadAsync(
        LendingActor actor, BearerCredential credential, CivilId customerId, DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!actor.MayReadCreditGrade) return new LoanForbidden();

        return await customers.FindAsync(credential, customerId, cancellationToken) switch
        {
            CustomerFound => new CreditGradeKnown(CreditGradePolicy.Evaluate(await store.ReadAsync(customerId, today),
                today)),
            CustomerNotFound missing => missing,
            LoanUnauthorized unauthorized => unauthorized,
            LoanForbidden forbidden => forbidden,
            LoanDependencyUnavailable unavailable => unavailable
        };
    }
}