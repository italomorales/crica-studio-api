namespace CricaStudio.Application.Auth;

public sealed record LoginResult(
    bool Succeeded,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAt = null,
    AuthenticatedUser? User = null)
{
    public static LoginResult InvalidCredentials() => new(false);
}

public sealed record AuthenticatedUser(Guid Id, string Email, string? Name);
