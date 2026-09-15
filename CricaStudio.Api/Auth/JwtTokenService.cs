using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CricaStudio.Application.Auth;
using CricaStudio.Domain.AdminUsers;
using Microsoft.IdentityModel.Tokens;

namespace CricaStudio.Api.Auth;

public sealed class JwtTokenService(JwtSettings settings) : IAccessTokenService
{
    public AccessToken Create(AdminUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(settings.ExpiresMinutes);
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim(JwtRegisteredClaimNames.Name, user.Name ?? user.Email)],
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
