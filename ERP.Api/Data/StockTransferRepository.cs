using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class StockTransferRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<List<TransferSourceOption>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT s.StockItemId, s.ProductName, s.ProductType, s.Quantity, s.Unit, s.Warehouse, r.RecipeName
FROM dbo.StockItems s LEFT JOIN dbo.PackagingRecipes r ON r.PackagingRecipeId = s.PackagingRecipeId
WHERE s.Quantity > 0 ORDER BY s.ProductName, s.Warehouse, s.StockItemId";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sources = new List<TransferSourceOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new TransferSourceOption
            {
                StockItemId = reader.GetInt32(0), ProductName = reader.GetString(1), ProductType = reader.GetString(2),
                Quantity = reader.GetDecimal(3), Unit = reader.GetString(4), Warehouse = reader.GetString(5),
                RecipeName = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return sources;
    }

    public async Task<List<TransferHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT TOP (100) t.ProductName, t.ProductType, t.Quantity, t.Unit, t.FromWarehouse,
t.ToWarehouse, CONCAT(u.Ad, N' ', u.SoyAd), t.CreatedAt
FROM dbo.StockTransfers t LEFT JOIN dbo.UserTable u ON u.UserId = t.TransferredByUserId
ORDER BY t.CreatedAt DESC, t.StockTransferId DESC";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var transfers = new List<TransferHistoryItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            transfers.Add(new TransferHistoryItem
            {
                ProductName = reader.GetString(0), ProductType = reader.GetString(1), Quantity = reader.GetDecimal(2),
                Unit = reader.GetString(3), FromWarehouse = reader.GetString(4), ToWarehouse = reader.GetString(5),
                TransferredBy = reader.IsDBNull(6) ? "Kullanıcı" : reader.GetString(6), CreatedAt = reader.GetDateTime(7)
            });
        }
        return transfers;
    }

    public async Task<string?> TransferAsync(int stockItemId, string toWarehouse, decimal quantity, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string sourceSql = @"SELECT ProductName, ProductType, Quantity, Unit, Warehouse, PackagingRecipeId
FROM dbo.StockItems WITH (UPDLOCK, HOLDLOCK) WHERE StockItemId = @StockItemId";
            string productName;
            string productType;
            decimal available;
            string unit;
            string fromWarehouse;
            int? recipeId;
            await using (var sourceCommand = new SqlCommand(sourceSql, connection, transaction))
            {
                sourceCommand.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                await using var reader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return "Seçilen stok kaydı bulunamadı.";
                productName = reader.GetString(0);
                productType = reader.GetString(1);
                available = reader.GetDecimal(2);
                unit = reader.GetString(3);
                fromWarehouse = reader.GetString(4);
                recipeId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            }

            if (toWarehouse == fromWarehouse) return "Kaynak ve hedef depo aynı olamaz.";
            if (quantity < 0.001m || quantity > available) return "Transfer miktarı mevcut stoktan fazla veya geçersiz.";

            const string destinationSql = @"SELECT TOP (1) StockItemId
FROM dbo.StockItems WITH (UPDLOCK, HOLDLOCK)
WHERE ProductName = @ProductName AND ProductType = @ProductType AND Unit = @Unit
  AND Warehouse = @Warehouse AND ((PackagingRecipeId = @RecipeId) OR (PackagingRecipeId IS NULL AND @RecipeId IS NULL))
ORDER BY StockItemId";
            int? destinationId;
            await using (var destinationCommand = new SqlCommand(destinationSql, connection, transaction))
            {
                destinationCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                destinationCommand.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                destinationCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                destinationCommand.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = toWarehouse;
                destinationCommand.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipeId.HasValue ? recipeId.Value : DBNull.Value;
                var value = await destinationCommand.ExecuteScalarAsync(cancellationToken);
                destinationId = value is null or DBNull ? null : Convert.ToInt32(value);
            }

            if (available == quantity)
            {
                await using var deleteSource = new SqlCommand("DELETE FROM dbo.StockItems WHERE StockItemId = @Id", connection, transaction);
                deleteSource.Parameters.Add("@Id", SqlDbType.Int).Value = stockItemId;
                await deleteSource.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var updateSource = new SqlCommand("UPDATE dbo.StockItems SET Quantity = Quantity - @Quantity WHERE StockItemId = @Id", connection, transaction);
                var sourceQuantity = updateSource.Parameters.Add("@Quantity", SqlDbType.Decimal);
                sourceQuantity.Precision = 18;
                sourceQuantity.Scale = 3;
                sourceQuantity.Value = quantity;
                updateSource.Parameters.Add("@Id", SqlDbType.Int).Value = stockItemId;
                await updateSource.ExecuteNonQueryAsync(cancellationToken);
            }

            if (destinationId.HasValue)
            {
                await using var updateDestination = new SqlCommand("UPDATE dbo.StockItems SET Quantity = Quantity + @Quantity WHERE StockItemId = @Id", connection, transaction);
                var destinationQuantity = updateDestination.Parameters.Add("@Quantity", SqlDbType.Decimal);
                destinationQuantity.Precision = 18;
                destinationQuantity.Scale = 3;
                destinationQuantity.Value = quantity;
                updateDestination.Parameters.Add("@Id", SqlDbType.Int).Value = destinationId.Value;
                await updateDestination.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertDestination = @"INSERT INTO dbo.StockItems
(ProductName, ProductType, Quantity, Unit, Warehouse, PackagingRecipeId)
VALUES (@ProductName, @ProductType, @Quantity, @Unit, @Warehouse, @RecipeId)";
                await using var insertCommand = new SqlCommand(insertDestination, connection, transaction);
                insertCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                insertCommand.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                insertCommand.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = quantity;
                insertCommand.Parameters["@Quantity"].Precision = 18;
                insertCommand.Parameters["@Quantity"].Scale = 3;
                insertCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                insertCommand.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = toWarehouse;
                insertCommand.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipeId.HasValue ? recipeId.Value : DBNull.Value;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string historySql = @"INSERT INTO dbo.StockTransfers
(ProductName, ProductType, Quantity, Unit, FromWarehouse, ToWarehouse, PackagingRecipeId, TransferredByUserId)
OUTPUT INSERTED.StockTransferId
VALUES (@ProductName, @ProductType, @Quantity, @Unit, @FromWarehouse, @ToWarehouse, @RecipeId, @UserId)";
            int transferId;
            await using (var historyCommand = new SqlCommand(historySql, connection, transaction))
            {
                historyCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                historyCommand.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                var amount = historyCommand.Parameters.Add("@Quantity", SqlDbType.Decimal);
                amount.Precision = 18;
                amount.Scale = 3;
                amount.Value = quantity;
                historyCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                historyCommand.Parameters.Add("@FromWarehouse", SqlDbType.NVarChar, 30).Value = fromWarehouse;
                historyCommand.Parameters.Add("@ToWarehouse", SqlDbType.NVarChar, 30).Value = toWarehouse;
                historyCommand.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipeId.HasValue ? recipeId.Value : DBNull.Value;
                historyCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                transferId = Convert.ToInt32(await historyCommand.ExecuteScalarAsync(cancellationToken));
            }

            const string movementSql = @"INSERT INTO dbo.StockMovements
(StockItemId, ProductName, ProductType, MovementType, Quantity, Unit, Warehouse, Reason, Note, PerformedByUserId, RelatedTransferId)
VALUES (@StockItemId, @ProductName, @ProductType, @MovementType, @Quantity, @Unit, @Warehouse,
        N'Depo transferi', N'Transfer no: ' + CONVERT(nvarchar(20), @TransferId), @UserId, @TransferId)";
            foreach (var movement in new[] { (Type: "Çıkış", Warehouse: fromWarehouse), (Type: "Giriş", Warehouse: toWarehouse) })
            {
                await using var movementCommand = new SqlCommand(movementSql, connection, transaction);
                movementCommand.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                movementCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                movementCommand.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                movementCommand.Parameters.Add("@MovementType", SqlDbType.NVarChar, 10).Value = movement.Type;
                var movementQuantity = movementCommand.Parameters.Add("@Quantity", SqlDbType.Decimal);
                movementQuantity.Precision = 18;
                movementQuantity.Scale = 3;
                movementQuantity.Value = quantity;
                movementCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                movementCommand.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = movement.Warehouse;
                movementCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                movementCommand.Parameters.Add("@TransferId", SqlDbType.Int).Value = transferId;
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
}
