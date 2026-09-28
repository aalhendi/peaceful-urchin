using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lending.Api.Application;
using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed record ActorResolved(LendingActor Actor, BearerCredential Credential);

internal sealed record ActorUnauthorized;

internal sealed record ActorUnavailable;

internal union ActorLookupResult(ActorResolved, ActorUnauthorized, ActorUnavailable);

internal sealed class AccessActorClient(IHttpClientFactory clientFactory)
{
    public async Task<ActorLookupResult> ResolveAsync(BearerCredential credential, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/actor");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ExposeSecret());

        try
        {
            using var response = await clientFactory.CreateClient("Access").SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new ActorUnauthorized();
            if (response.StatusCode != HttpStatusCode.OK) return new ActorUnavailable();

            var actor = await response.Content.ReadFromJsonAsync<ActorResponse>(cancellationToken);
            if (actor is null || actor.Permissions is null ||
                StaffActorId.Parse(actor.ActorId).Value is not StaffActorId actorId ||
                InstitutionKind.Parse(actor.InstitutionKind).Value is not InstitutionKind kind ||
                InstitutionId.Parse(actor.InstitutionId).Value is not InstitutionId institutionId)
                return new ActorUnavailable();

            return new ActorResolved(new LendingActor(actorId, institutionId, kind,
                actor.Permissions.ToHashSet(StringComparer.Ordinal)), credential);
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