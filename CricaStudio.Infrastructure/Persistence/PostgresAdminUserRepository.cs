using CricaStudio.Domain.AdminUsers;
using Npgsql;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresAdminUserRepository(string connectionString) : IAdminUserRepository
{
    public async Task<AdminUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, email, name, password_hash, password_salt, password_iterations, is_active
            FROM cricastudio.admin_users
            WHERE email = @email
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new AdminUser(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetBoolean(6));
    }
}
