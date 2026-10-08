using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class UserRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<UserTable?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT UserId, [Password], AktifPasif, Ad, SoyAd, Mail FROM dbo.UserTable WHERE Mail = @Mail";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Mail", SqlDbType.NVarChar, 320).Value = email;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new UserTable
        {
            UserId = reader.GetInt32(0),
            PasswordHash = reader.GetString(1),
            AktifPasif = reader.GetBoolean(2),
            Ad = reader.GetString(3),
            SoyAd = reader.GetString(4),
            Mail = reader.GetString(5)
        };
    }

    public async Task<int> CreateAsync(UserTable user, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO dbo.UserTable ([Password], AktifPasif, Ad, SoyAd, Mail) OUTPUT INSERTED.UserId VALUES (@Password, @AktifPasif, @Ad, @SoyAd, @Mail)";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = user.PasswordHash;
        command.Parameters.Add("@AktifPasif", SqlDbType.Bit).Value = user.AktifPasif;
        command.Parameters.Add("@Ad", SqlDbType.NVarChar, 100).Value = user.Ad;
        command.Parameters.Add("@SoyAd", SqlDbType.NVarChar, 100).Value = user.SoyAd;
        command.Parameters.Add("@Mail", SqlDbType.NVarChar, 320).Value = user.Mail;
        await connection.OpenAsync(cancellationToken);
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Kullanıcı oluşturulamadı."));
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.UserTable SET [Password] = @Password WHERE UserId = @UserId AND AktifPasif = 1";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = passwordHash;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
