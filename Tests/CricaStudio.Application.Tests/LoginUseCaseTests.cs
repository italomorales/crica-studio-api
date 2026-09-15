using CricaStudio.Application.Auth;
using CricaStudio.Domain.AdminUsers;
using CricaStudio.Infrastructure.Security;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class LoginUseCaseTests
{
    [Fact]
    public async Task Valid_credentials_return_access_token()
    {
        var password = Pbkdf2PasswordHasher.Create("senha-segura");
        var user = new AdminUser(Guid.NewGuid(), "admin@crica.com", "Admin", password.Hash, password.Salt, password.Iterations, true);
        var useCase = new LoginUseCase(new StubRepository(user), new Pbkdf2PasswordHasher(), new StubTokenService());

        var result = await useCase.ExecuteAsync(new LoginCommand(" ADMIN@CRICA.COM ", "senha-segura"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("token-de-teste", result.AccessToken);
        Assert.Equal(user.Email, result.User!.Email);
    }

    [Theory]
    [InlineData("senha-incorreta", true)]
    [InlineData("senha-segura", false)]
    public async Task Invalid_or_inactive_user_is_rejected(string attemptedPassword, bool isActive)
    {
        var password = Pbkdf2PasswordHasher.Create("senha-segura");
        var user = new AdminUser(Guid.NewGuid(), "admin@crica.com", null, password.Hash, password.Salt, password.Iterations, isActive);
        var useCase = new LoginUseCase(new StubRepository(user), new Pbkdf2PasswordHasher(), new StubTokenService());

        var result = await useCase.ExecuteAsync(new LoginCommand(user.Email, attemptedPassword), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    private sealed class StubRepository(AdminUser? user) : IAdminUserRepository
    {
        public Task<AdminUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(user?.Email == email ? user : null);
    }

    private sealed class StubTokenService : IAccessTokenService
    {
        public AccessToken Create(AdminUser user) => new("token-de-teste", DateTimeOffset.UtcNow.AddHours(1));
    }
}
