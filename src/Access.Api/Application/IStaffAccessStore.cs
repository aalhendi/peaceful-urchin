using System.Collections.Immutable;
using Access.Api.Domain;

namespace Access.Api.Application;

internal sealed record StaffCredentials(StaffUser User, string PasswordHash);

internal interface IStaffAccessStore
{
    Task<StaffCredentials?> FindCredentialsAsync(StaffUserName userName);
    Task<StaffUser?> FindUserAsync(StaffUserId userId);
    Task InsertSessionAsync(StaffSessionToken token, StaffUserId userId, DateTimeOffset expiresAt);
    Task<StaffActor?> ResolveSessionAsync(StaffSessionToken token);
    Task<bool> RevokeSessionAsync(StaffSessionToken token);
    Task ReplaceRolesAsync(StaffUserId userId, ImmutableArray<StaffRole> roles);
}