using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class StockMovementRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<List<StockMovementItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT TOP (200) m.CreatedAt, m.ProductName, m.ProductType, m.MovementType,
m.Quantity, m.Unit, m.Warehouse, m.Reason, m.Note, CONCAT(u.Ad, N' ', u.SoyAd)
FROM dbo.StockMovements m LEFT JOIN dbo.UserTable u ON u.UserId = m.PerformedByUserId
ORDER BY m.CreatedAt DESC, m.StockMovementId DESC";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var movements = new List<StockMovementItemViewModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            movements.Add(new StockMovementItemViewModel
            {
                CreatedAt = reader.GetDateTime(0), ProductName = reader.GetString(1), ProductType = reader.GetString(2),
                MovementType = reader.GetString(3), Quantity = reader.GetDecimal(4), Unit = reader.GetString(5),
                Warehouse = reader.GetString(6), Reason = reader.GetString(7), Note = reader.IsDBNull(8) ? null : reader.GetString(8),
                PerformedBy = reader.IsDBNull(9) ? "Kullanıcı" : reader.GetString(9)
            });
        }
        return movements;
    }

    public async Task<string?> RecordAsync(int stockItemId, string movementType, decimal quantity, string reason, string? note, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string sourceSql = @"SELECT ProductName, ProductType, Quantity, Unit, Warehouse
FROM dbo.StockItems WITH (UPDLOCK, HOLDLOCK) WHERE StockItemId = @StockItemId";
            string productName;
            string productType;
            decimal available;
            string unit;
            string warehouse;
            await using (var sourceCommand = new SqlCommand(sourceSql, connection, transaction))
            {
                sourceCommand.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                await using var reader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return "Seçilen stok kaydı bulunamadı.";
                productName = reader.GetString(0);
                productType = reader.GetString(1);
                available = reader.GetDecimal(2);
                unit = reader.GetString(3);
                warehouse = reader.GetString(4);
            }

            if (movementType == "Çıkış" && quantity > available)
                return "Çıkış miktarı mevcut stoktan fazla olamaz.";

            if (movementType == "Giriş")
            {
                await using var update = new SqlCommand("UPDATE dbo.StockItems SET Quantity = Quantity + @Quantity WHERE StockItemId = @StockItemId", connection, transaction);
                AddAmount(update, quantity);
                update.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            else if (quantity == available)
            {
                await using var delete = new SqlCommand("DELETE FROM dbo.StockItems WHERE StockItemId = @StockItemId", connection, transaction);
                delete.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var update = new SqlCommand("UPDATE dbo.StockItems SET Quantity = Quantity - @Quantity WHERE StockItemId = @StockItemId", connection, transaction);
                AddAmount(update, quantity);
                update.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            const string movementSql = @"INSERT INTO dbo.StockMovements
(StockItemId, ProductName, ProductType, MovementType, Quantity, Unit, Warehouse, Reason, Note, PerformedByUserId)
VALUES (@StockItemId, @ProductName, @ProductType, @MovementType, @Quantity, @Unit, @Warehouse, @Reason, @Note, @UserId)";
            await using (var movementCommand = new SqlCommand(movementSql, connection, transaction))
            {
                movementCommand.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                movementCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                movementCommand.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                movementCommand.Parameters.Add("@MovementType", SqlDbType.NVarChar, 10).Value = movementType;
                AddAmount(movementCommand, quantity);
                movementCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                movementCommand.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = warehouse;
                movementCommand.Parameters.Add("@Reason", SqlDbType.NVarChar, 50).Value = reason;
                movementCommand.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim();
                movementCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await movementCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddAmount(SqlCommand command, decimal quantity)
    {
        var parameter = command.Parameters.Add("@Quantity", SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = quantity;
    }
}
