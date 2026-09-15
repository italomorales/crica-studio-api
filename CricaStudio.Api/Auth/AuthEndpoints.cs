using CricaStudio.Application.Auth;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;

namespace CricaStudio.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/auth");
        group.MapPost("/login", async (LoginRequest request, LoginUseCase login, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["E-mail é obrigatório."], ["password"] = ["Senha é obrigatória."],
                });
            }

            var result = await login.ExecuteAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
            return result.Succeeded
                ? Results.Ok(new LoginResponse(result.AccessToken!, result.ExpiresAt!.Value, result.User!))
                : Results.Unauthorized();
        });

        group.MapGet("/me", [Authorize] (HttpContext context) => Results.Ok(new
        {
            id = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            email = context.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            name = context.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value,
        }));
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthenticatedUser User);
