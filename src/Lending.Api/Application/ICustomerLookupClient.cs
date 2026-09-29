using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal sealed record CustomerFound;

internal sealed record CustomerNotFound;

internal sealed record LoanUnauthorized;

internal sealed record LoanDependencyUnavailable;

internal union CustomerLookupResult(
    CustomerFound, CustomerNotFound, LoanUnauthorized, LoanForbidden, LoanDependencyUnavailable);

internal interface ICustomerLookupClient
{
    Task<CustomerLookupResult> FindAsync(
        BearerCredential credential, CivilId customerId, CancellationToken cancellationToken);
}