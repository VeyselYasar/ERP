using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class CustomerRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<CustomerPageViewModel> GetPageAsync(int? customerId, bool canRead, bool canWrite, CancellationToken cancellationToken = default)
    {
        var page = new CustomerPageViewModel { CanRead = canRead, CanWrite = canWrite };
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (canRead)
        {
            const string listSql = @"SELECT CustomerId, CustomerCode, CustomerType, Title, ContactName, Phone, Email, IsActive
FROM dbo.Customers ORDER BY IsActive DESC, Title";
            await using var command = new SqlCommand(listSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                page.Customers.Add(new CustomerCardViewModel
                {
                    CustomerId = reader.GetInt32(0), CustomerCode = reader.GetString(1), CustomerType = reader.GetString(2),
                    Title = reader.GetString(3), ContactName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Phone = reader.IsDBNull(5) ? null : reader.GetString(5), Email = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsActive = reader.GetBoolean(7)
                });
            }
        }

        if (canWrite && customerId.HasValue)
        {
            const string detailSql = @"SELECT CustomerId, CustomerCode, CustomerType, Title, TaxOffice, TaxNumber,
ContactName, Phone, Email, BillingAddress, DeliveryAddress, IsActive
FROM dbo.Customers WHERE CustomerId = @CustomerId";
            await using var command = new SqlCommand(detailSql, connection);
            command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId.Value;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                page.Form = new CustomerCardViewModel
                {
                    CustomerId = reader.GetInt32(0), CustomerCode = reader.GetString(1), CustomerType = reader.GetString(2),
                    Title = reader.GetString(3), TaxOffice = reader.IsDBNull(4) ? null : reader.GetString(4),
                    TaxNumber = reader.IsDBNull(5) ? null : reader.GetString(5), ContactName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Phone = reader.IsDBNull(7) ? null : reader.GetString(7), Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                    BillingAddress = reader.IsDBNull(9) ? null : reader.GetString(9), DeliveryAddress = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsActive = reader.GetBoolean(11)
                };
            }
        }
        return page;
    }

    public async Task SaveAsync(CustomerCardViewModel customer, int userId, CancellationToken cancellationToken = default)
    {
        const string updateSql = @"UPDATE dbo.Customers SET CustomerCode = @CustomerCode, CustomerType = @CustomerType,
Title = @Title, TaxOffice = @TaxOffice, TaxNumber = @TaxNumber, ContactName = @ContactName,
Phone = @Phone, Email = @Email, BillingAddress = @BillingAddress, DeliveryAddress = @DeliveryAddress,
IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME(), UpdatedByUserId = @UserId
WHERE CustomerId = @CustomerId;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.Customers
        (CustomerCode, CustomerType, Title, TaxOffice, TaxNumber, ContactName, Phone, Email, BillingAddress, DeliveryAddress, IsActive, CreatedByUserId, UpdatedByUserId)
    VALUES
        (@CustomerCode, @CustomerType, @Title, @TaxOffice, @TaxNumber, @ContactName, @Phone, @Email, @BillingAddress, @DeliveryAddress, @IsActive, @UserId, @UserId);";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(updateSql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customer.CustomerId;
        AddText(command, "@CustomerCode", 30, customer.CustomerCode);
        AddText(command, "@CustomerType", 20, customer.CustomerType);
        AddText(command, "@Title", 200, customer.Title);
        AddText(command, "@TaxOffice", 100, customer.TaxOffice);
        AddText(command, "@TaxNumber", 30, customer.TaxNumber);
        AddText(command, "@ContactName", 150, customer.ContactName);
        AddText(command, "@Phone", 30, customer.Phone);
        AddText(command, "@Email", 320, customer.Email);
        AddText(command, "@BillingAddress", 500, customer.BillingAddress);
        AddText(command, "@DeliveryAddress", 500, customer.DeliveryAddress);
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = customer.IsActive;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> SetActiveAsync(int customerId, bool isActive, int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE dbo.Customers
SET IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME(), UpdatedByUserId = @UserId
WHERE CustomerId = @CustomerId";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void AddText(SqlCommand command, string name, int size, string? value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
