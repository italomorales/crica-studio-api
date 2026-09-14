namespace CricaStudio.Domain.AdminUsers;

public sealed record AdminUser(
    Guid Id,
    string Email,
    string? Name,
    string PasswordHash,
    string PasswordSalt,
    int PasswordIterations,
    bool IsActive);
