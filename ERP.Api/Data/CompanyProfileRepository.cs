using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class CompanyProfileRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<CompanyProfileViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT CompanyName, TaxOffice, TaxNumber, Phone, Email, CompanyAddress, FactoryName, FactoryAddress, UpdatedAt
FROM dbo.CompanyProfile WHERE ProfileId = 1";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new CompanyProfileViewModel();
        return new CompanyProfileViewModel
        {
            Exists = true,
            CompanyName = reader.GetString(0),
            TaxOffice = reader.IsDBNull(1) ? null : reader.GetString(1),
            TaxNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
            Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
            Email = reader.IsDBNull(4) ? null : reader.GetString(4),
            CompanyAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
            FactoryName = reader.IsDBNull(6) ? null : reader.GetString(6),
            FactoryAddress = reader.IsDBNull(7) ? null : reader.GetString(7),
            UpdatedAt = reader.GetDateTime(8)
        };
    }

    public async Task SaveAsync(CompanyProfileViewModel profile, int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE dbo.CompanyProfile
SET CompanyName = @CompanyName, TaxOffice = @TaxOffice, TaxNumber = @TaxNumber,
    Phone = @Phone, Email = @Email, CompanyAddress = @CompanyAddress,
    FactoryName = @FactoryName, FactoryAddress = @FactoryAddress,
    UpdatedAt = SYSUTCDATETIME(), UpdatedByUserId = @UserId
WHERE ProfileId = 1;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.CompanyProfile
        (ProfileId, CompanyName, TaxOffice, TaxNumber, Phone, Email, CompanyAddress, FactoryName, FactoryAddress, UpdatedByUserId)
    VALUES
        (1, @CompanyName, @TaxOffice, @TaxNumber, @Phone, @Email, @CompanyAddress, @FactoryName, @FactoryAddress, @UserId);";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        AddText(command, "@CompanyName", 200, profile.CompanyName.Trim());
        AddText(command, "@TaxOffice", 100, profile.TaxOffice);
        AddText(command, "@TaxNumber", 30, profile.TaxNumber);
        AddText(command, "@Phone", 30, profile.Phone);
        AddText(command, "@Email", 320, profile.Email);
        AddText(command, "@CompanyAddress", 500, profile.CompanyAddress);
        AddText(command, "@FactoryName", 200, profile.FactoryName);
        AddText(command, "@FactoryAddress", 500, profile.FactoryAddress);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddText(SqlCommand command, string name, int length, string? value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value.Trim();
    }
}
