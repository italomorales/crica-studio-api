namespace CricaStudio.Domain.AdminUsers;

public interface IAdminUserRepository
{
    Task<AdminUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
