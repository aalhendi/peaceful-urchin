using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Credit.Api.Application;
using Credit.Api.Domain;

namespace Credit.Api.Infrastructure;

internal sealed record ActorResolved(CreditActor Actor);

internal sealed record ActorUnauthorized;

internal sealed record ActorUnavailable;

internal union ActorLookupResult(ActorResolved, ActorUnauthorized, ActorUnavailable);

internal sealed class AccessActorClient(IHttpClientFactory clientFactory)
{
    public async Task<ActorLookupResult> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/actor");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await clientFactory.CreateClient("Access").SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new ActorUnauthorized();
            if (response.StatusCode != HttpStatusCode.OK) return new ActorUnavailable();

            var actor = await response.Content.ReadFromJsonAsync<ActorResponse>(cancellationToken);
            if (actor is null || actor.ActorId == Guid.Empty || actor.InstitutionId == Guid.Empty ||
                actor.Permissions.Length == 0 ||
                InstitutionKind.Parse(actor.InstitutionKind).Value is not InstitutionKind kind)
                return new ActorUnavailable();

            return new ActorResolved(new CreditActor(kind, actor.Permissions.ToHashSet(StringComparer.Ordinal)));
        }
        catch (HttpRequestException)
        {
            return new ActorUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ActorUnavailable();
        }
        catch (JsonException)
        {
            return new ActorUnavailable();
        }
    }

    private sealed record ActorResponse(
        Guid ActorId,
        Guid InstitutionId,
        string InstitutionKind,
        string[] Permissions);
}