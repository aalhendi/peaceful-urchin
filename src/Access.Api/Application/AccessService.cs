using System.Collections.Immutable;
using Access.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Shared.Vocabulary;

namespace Access.Api.Application;

internal sealed record LoginSuccess(StaffSessionToken Token, DateTimeOffset ExpiresAt);

internal enum ChangeRolesResult
{
    Changed,
    Forbidden,
    NotFound,
    InvalidRoles
}

internal sealed class AccessService(IStaffAccessStore store, IPasswordHasher<StaffUser> passwordHasher)
{
    // NOTE(aalhendi): Unknown usernames use a dummy hash so they still pay the password verification cost.
    private static readonly StaffUser DummyUser = new(
        StaffUserId.New(),
        StaffUserName.Parse("unknown-user").Value as StaffUserName
        ?? throw new InvalidOperationException("Dummy username is invalid."),
        InstitutionId.New(), InstitutionKind.Bank, false);

    private static readonly string DummyHash = new PasswordHasher<StaffUser>()
        .HashPassword(DummyUser, "unknown-password");

    public async Task<LoginSuccess?> LoginAsync(StaffUserName userName, StaffPassword password)
    {
        var credentials = await store.FindCredentialsAsync(userName);
        var verification = passwordHasher.VerifyHashedPassword(
            credentials?.User ?? DummyUser,
            credentials?.PasswordHash ?? DummyHash,
            password.ExposeSecret());
        if (credentials is null || !credentials.User.Active || verification == PasswordVerificationResult.Failed)
            return null;

        var token = StaffSessionToken.New();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        await store.InsertSessionAsync(token, credentials.User.Id, expiresAt);
        return new LoginSuccess(token, expiresAt);
    }

    public Task<StaffActor?> ResolveAsync(StaffSessionToken token) => store.ResolveSessionAsync(token);

    public Task<bool> LogoutAsync(StaffSessionToken token) => store.RevokeSessionAsync(token);

    public async Task<ChangeRolesResult> ChangeRolesAsync(
        StaffActor actor, StaffUserId targetId, ImmutableArray<StaffRole> roles)
    {
        if (actor.InstitutionKind != InstitutionKind.Cinet || !actor.HasRole(StaffRole.AccessAdmin))
            return ChangeRolesResult.Forbidden;

        var target = await store.FindUserAsync(targetId);
        if (target is null) return ChangeRolesResult.NotFound;
        if (roles.Any(role => !role.IsAllowedFor(target.InstitutionKind)))
            return ChangeRolesResult.InvalidRoles;

        await store.ReplaceRolesAsync(targetId, roles);
        return ChangeRolesResult.Changed;
    }
}