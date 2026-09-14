using System.Security.Cryptography;
using CricaStudio.Domain.AdminUsers;

namespace CricaStudio.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 210_000;
    private const int HashSize = 32;

    public bool Verify(string password, string hash, string salt, int iterations)
    {
        if (string.IsNullOrEmpty(password) || iterations < 1) return false;

        try
        {
            var expectedHash = Convert.FromBase64String(hash);
            var saltBytes = Convert.FromBase64String(salt);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                iterations,
                HashAlgorithmName.SHA512,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static PasswordHash Create(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA512,
            HashSize);

        return new PasswordHash(Convert.ToBase64String(hash), Convert.ToBase64String(salt), DefaultIterations);
    }
}

public sealed record PasswordHash(string Hash, string Salt, int Iterations);
