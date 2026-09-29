using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lending.Api.Application;
using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class CreditCustomerClient(IHttpClientFactory clientFactory) : ICustomerLookupClient
{
    public async Task<CustomerLookupResult> FindAsync(
        BearerCredential credential, CivilId customerId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "customers/lookup");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ExposeSecret());
        request.Content = JsonContent.Create(new { CustomerCivilId = customerId.ExposeSecret() });

        try
        {
            using var response = await clientFactory.CreateClient("Credit").SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new CustomerNotFound();
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new LoanUnauthorized();
            if (response.StatusCode == HttpStatusCode.Forbidden) return new LoanForbidden();
            return response.StatusCode == HttpStatusCode.NoContent
                ? new CustomerFound()
                : new LoanDependencyUnavailable();
        }
        catch (HttpRequestException)
        {
            return new LoanDependencyUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LoanDependencyUnavailable();
        }
    }
}