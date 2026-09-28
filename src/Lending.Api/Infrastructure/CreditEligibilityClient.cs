using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lending.Api.Application;
using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class CreditEligibilityClient(IHttpClientFactory clientFactory) : ICustomerEligibilityClient
{
    public async Task<CustomerEligibilityResult> CheckAsync(
        BearerCredential credential, CivilId customerId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "customers/eligibility/check");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ExposeSecret());
        request.Content = JsonContent.Create(new { CustomerCivilId = customerId.ExposeSecret() });

        try
        {
            using var response = await clientFactory.CreateClient("Credit").SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new CustomerNotFound();
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new LoanUnauthorized();
            if (response.StatusCode == HttpStatusCode.Forbidden) return new LoanForbidden();
            if (response.StatusCode != HttpStatusCode.OK) return new LoanDependencyUnavailable();

            var result = await response.Content.ReadFromJsonAsync<EligibilityResponse>(cancellationToken);
            return result?.Eligible switch
            {
                true => new CustomerEligible(),
                false => new CustomerIneligible(),
                null => new LoanDependencyUnavailable()
            };
        }
        catch (HttpRequestException)
        {
            return new LoanDependencyUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LoanDependencyUnavailable();
        }
        catch (JsonException)
        {
            return new LoanDependencyUnavailable();
        }
    }

    private sealed record EligibilityResponse(bool? Eligible);
}