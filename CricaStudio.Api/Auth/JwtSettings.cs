namespace CricaStudio.Api.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiresMinutes { get; init; } = 480;

    public void Validate()
    {
        if (Key.Length < 32) throw new InvalidOperationException("Jwt:Key precisa ter ao menos 32 caracteres.");
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)) throw new InvalidOperationException("Jwt:Issuer e Jwt:Audience são obrigatórios.");
        if (ExpiresMinutes is < 1 or > 1_440) throw new InvalidOperationException("Jwt:ExpiresMinutes deve estar entre 1 e 1440.");
    }
}
