using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record CustomerEligible;

internal sealed record CustomerIneligible;

internal sealed record CustomerNotFound;

internal sealed record LoanUnauthorized;

internal sealed record LoanDependencyUnavailable;

internal union CustomerEligibilityResult(
    CustomerEligible, CustomerIneligible, CustomerNotFound, LoanUnauthorized, LoanForbidden, LoanDependencyUnavailable);

internal interface ICustomerEligibilityClient
{
    Task<CustomerEligibilityResult> CheckAsync(
        BearerCredential credential, CivilId customerId, CancellationToken cancellationToken);
}