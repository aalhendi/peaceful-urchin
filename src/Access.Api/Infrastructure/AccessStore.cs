using System.Collections.Immutable;
using Access.Api.Application;
using Access.Api.Domain;
using Dapper;
using Npgsql;
using Shared.Vocabulary;

namespace Access.Api.Infrastructure;

internal sealed class AccessStore(NpgsqlDataSource dataSource) : IStaffAccessStore
{
    public async Task<StaffCredentials?> FindCredentialsAsync(StaffUserName userName)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<StaffUserRow>("""
                                                                           SELECT u.id AS "Id", u.username AS "UserName", u.password_hash AS "PasswordHash",
                                                                                  u.institution_id AS "InstitutionId", i.kind AS "Kind", u.active AS "Active"
                                                                           FROM staff_users u
                                                                           JOIN institutions i ON i.id = u.institution_id
                                                                           WHERE u.username = @UserName
                                                                           """, new { UserName = userName.Value });
        return row is null ? null : new StaffCredentials(MapUser(row), row.PasswordHash);
    }

    public async Task<StaffUser?> FindUserAsync(StaffUserId userId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<StaffUserRow>("""
                                                                           SELECT u.id AS "Id", u.username AS "UserName", u.password_hash AS "PasswordHash",
                                                                                  u.institution_id AS "InstitutionId", i.kind AS "Kind", u.active AS "Active"
                                                                           FROM staff_users u
                                                                           JOIN institutions i ON i.id = u.institution_id
                                                                           WHERE u.id = @UserId
                                                                           """, new { UserId = userId.Value });
        return row is null ? null : MapUser(row);
    }

    public async Task InsertSessionAsync(StaffSessionToken token, StaffUserId userId, DateTimeOffset expiresAt)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
                                      INSERT INTO staff_sessions (token_digest, staff_user_id, expires_at)
                                      VALUES (@Digest, @UserId, @ExpiresAt)
                                      """,
            new { Digest = token.Digest(), UserId = userId.Value, ExpiresAt = expiresAt });
    }

    public async Task<StaffActor?> ResolveSessionAsync(StaffSessionToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var rows = (await connection.QueryAsync<ActorRow>("""
                                                          SELECT u.id AS "UserId", u.institution_id AS "InstitutionId", i.kind AS "Kind",
                                                                 r.role AS "Role"
                                                          FROM staff_sessions s
                                                          JOIN staff_users u ON u.id = s.staff_user_id
                                                          JOIN institutions i ON i.id = u.institution_id
                                                          LEFT JOIN staff_user_roles r ON r.staff_user_id = u.id
                                                          WHERE s.token_digest = @Digest AND s.expires_at > now()
                                                            AND s.revoked_at IS NULL AND u.active
                                                          ORDER BY r.role
                                                          """, new { Digest = token.Digest() })).ToList();
        if (rows.Count == 0) return null;

        var userId = StaffUserId.Parse(rows[0].UserId).Value as StaffUserId
                     ?? throw new InvalidOperationException("Stored staff user ID is invalid.");
        var institutionId = InstitutionId.Parse(rows[0].InstitutionId).Value as InstitutionId
                            ?? throw new InvalidOperationException("Stored institution ID is invalid.");
        var kind = InstitutionKind.Parse(rows[0].Kind).Value as InstitutionKind
                   ?? throw new InvalidOperationException("Stored institution kind is invalid.");
        var roles = rows.Where(row => row.Role is not null)
            .Select(row => StaffRole.Parse(row.Role).Value as StaffRole
                           ?? throw new InvalidOperationException("Stored staff role is invalid."))
            .ToImmutableArray();
        if (roles.Any(role => !role.IsAllowedFor(kind)))
            throw new InvalidOperationException("Stored staff role is incompatible with the institution.");
        return new StaffActor(userId, institutionId, kind, roles);
    }

    public async Task<bool> RevokeSessionAsync(StaffSessionToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteAsync("""
                                             UPDATE staff_sessions SET revoked_at = now()
                                             WHERE token_digest = @Digest AND revoked_at IS NULL AND expires_at > now()
                                             """, new { Digest = token.Digest() }) == 1;
    }

    public async Task ReplaceRolesAsync(StaffUserId userId, ImmutableArray<StaffRole> roles)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.QuerySingleAsync<Guid>("""
                                                SELECT id FROM staff_users WHERE id = @UserId FOR UPDATE
                                                """, new { UserId = userId.Value }, transaction);
        await connection.ExecuteAsync("""
                                      DELETE FROM staff_user_roles WHERE staff_user_id = @UserId
                                      """, new { UserId = userId.Value }, transaction);
        foreach (var role in roles)
            await connection.ExecuteAsync("""
                                          INSERT INTO staff_user_roles (staff_user_id, role) VALUES (@UserId, @Role)
                                          """, new { UserId = userId.Value, Role = role.Code }, transaction);
        await transaction.CommitAsync();
    }

    private static StaffUser MapUser(StaffUserRow row) => new(
        StaffUserId.Parse(row.Id).Value as StaffUserId
        ?? throw new InvalidOperationException("Stored staff user ID is invalid."),
        StaffUserName.Parse(row.UserName).Value as StaffUserName
        ?? throw new InvalidOperationException("Stored username is invalid."),
        InstitutionId.Parse(row.InstitutionId).Value as InstitutionId
        ?? throw new InvalidOperationException("Stored institution ID is invalid."),
        InstitutionKind.Parse(row.Kind).Value as InstitutionKind
        ?? throw new InvalidOperationException("Stored institution kind is invalid."),
        row.Active);

    private sealed record StaffUserRow(
        Guid Id,
        string UserName,
        string PasswordHash,
        Guid InstitutionId,
        string Kind,
        bool Active);

    private sealed record ActorRow(Guid UserId, Guid InstitutionId, string Kind, string? Role);
}