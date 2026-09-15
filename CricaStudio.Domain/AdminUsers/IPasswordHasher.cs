namespace CricaStudio.Domain.AdminUsers;

public interface IPasswordHasher
{
    bool Verify(string password, string hash, string salt, int iterations);
}
