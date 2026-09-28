namespace Access.Api.Web.Contracts;

internal sealed class LoginRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
}

internal sealed record LoginResponse(string TokenType, string AccessToken, DateTimeOffset ExpiresAt);

internal sealed record ReplaceRolesRequest(string[]? Roles);

internal sealed record ActorResponse(
    Guid ActorId,
    Guid InstitutionId,
    string InstitutionKind,
    string[] Roles,
    string[] Permissions);
