using CricaStudio.Domain.AdminUsers;

namespace CricaStudio.Application.Auth;

public sealed class LoginUseCase(
    IAdminUserRepository users,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens)
{
    public async Task<LoginResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive ||
            !passwordHasher.Verify(command.Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            return LoginResult.InvalidCredentials();
        }

        var token = accessTokens.Create(user);
        return new LoginResult(
            true,
            token.Value,
            token.ExpiresAt,
            new AuthenticatedUser(user.Id, user.Email, user.Name));
    }
}
