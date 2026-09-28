using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record LendingActor(
    StaffActorId Id,
    InstitutionId InstitutionId,
    InstitutionKind InstitutionKind,
    IReadOnlySet<string> Permissions)
{
    public bool MayCreateLoan => InstitutionKind == InstitutionKind.Bank && Permissions.Contains("Loan.Create");
    public bool MayBlockLoans => InstitutionKind == InstitutionKind.Cinet && Permissions.Contains("Loan.Block");
    public bool MayUploadPayments => InstitutionKind == InstitutionKind.Bank && Permissions.Contains("Payment.Write");
    public bool MayReadCustomerLoans => Permissions.Contains("Customer.Read");
}

internal sealed record CreateLoanCommand(
    CivilId CustomerId,
    DateOnly StartDate,
    LoanTenor Tenor,
    LoanPrincipal Principal,
    FinancingRate Rate,
    RepaymentSchedule Schedule);

internal sealed record LoanCreated(LoanId Id);

internal sealed record CustomerBlocked;

internal sealed record LoanForbidden;

internal union CreateLoanOutcome(
    LoanCreated, CustomerBlocked, CustomerIneligible, CustomerNotFound,
    LoanUnauthorized, LoanForbidden, LoanDependencyUnavailable);

internal sealed record LoanBlockChanged;

internal union ChangeLoanBlockOutcome(
    LoanBlockChanged, CustomerNotFound, LoanUnauthorized, LoanForbidden, LoanDependencyUnavailable);

internal sealed class LendingService(ILendingStore store, ICustomerEligibilityClient customers)
{
    public async Task<CreateLoanOutcome> CreateLoanAsync(
        LendingActor actor, BearerCredential credential, CreateLoanCommand command, CancellationToken cancellationToken)
    {
        if (!actor.MayCreateLoan) return new LoanForbidden();

        return await customers.CheckAsync(credential, command.CustomerId, cancellationToken) switch
        {
            CustomerEligible => await PersistLoanAsync(actor, command),
            CustomerIneligible ineligible => ineligible,
            CustomerNotFound missing => missing,
            LoanUnauthorized unauthorized => unauthorized,
            LoanForbidden forbidden => forbidden,
            LoanDependencyUnavailable unavailable => unavailable
        };
    }

    public async Task<ChangeLoanBlockOutcome> SetLoanBlockAsync(
        LendingActor actor, BearerCredential credential, CivilId customerId, bool blocked,
        CancellationToken cancellationToken)
    {
        if (!actor.MayBlockLoans) return new LoanForbidden();

        return await customers.CheckAsync(credential, customerId, cancellationToken) switch
        {
            CustomerEligible => await PersistBlockAsync(actor, customerId, blocked),
            CustomerIneligible => await PersistBlockAsync(actor, customerId, blocked),
            CustomerNotFound missing => missing,
            LoanUnauthorized unauthorized => unauthorized,
            LoanForbidden forbidden => forbidden,
            LoanDependencyUnavailable unavailable => unavailable
        };
    }

    private async Task<CreateLoanOutcome> PersistLoanAsync(LendingActor actor, CreateLoanCommand command)
    {
        var loan = new Loan(LoanId.New(), command.CustomerId, actor.InstitutionId,
            command.StartDate, command.Tenor, command.Principal, command.Rate, LoanStatus.Open, command.Schedule);
        return await store.CreateLoanAsync(loan) ? new LoanCreated(loan.Id) : new CustomerBlocked();
    }

    private async Task<ChangeLoanBlockOutcome> PersistBlockAsync(LendingActor actor, CivilId customerId, bool blocked)
    {
        await store.SetLoanBlockAsync(customerId, blocked, actor.Id);
        return new LoanBlockChanged();
    }
}