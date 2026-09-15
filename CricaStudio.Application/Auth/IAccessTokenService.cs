using CricaStudio.Domain.AdminUsers;

namespace CricaStudio.Application.Auth;

public interface IAccessTokenService
{
    AccessToken Create(AdminUser user);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
