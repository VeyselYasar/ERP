using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class ProductionRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<List<ProductionRecordItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT TOP (200) p.ProducedAt, p.ProductName, p.Quantity, p.Unit, p.Warehouse,
r.RecipeName, CONCAT(u.Ad, N' ', u.SoyAd), p.Note
FROM dbo.ProductionRecords p
INNER JOIN dbo.PackagingRecipes r ON r.PackagingRecipeId = p.PackagingRecipeId
LEFT JOIN dbo.UserTable u ON u.UserId = p.ProducedByUserId
ORDER BY p.ProducedAt DESC, p.ProductionRecordId DESC";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<ProductionRecordItemViewModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ProductionRecordItemViewModel
            {
                ProducedAt = reader.GetDateTime(0), ProductName = reader.GetString(1), Quantity = reader.GetDecimal(2),
                Unit = reader.GetString(3), Warehouse = reader.GetString(4), RecipeName = reader.GetString(5),
                ProducedBy = reader.IsDBNull(6) ? "Kullanıcı" : reader.GetString(6), Note = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return records;
    }

    public async Task RecordAsync(ProductionPageViewModel model, PackagingRecipeItemViewModel recipe, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string productionSql = @"INSERT INTO dbo.ProductionRecords
(ProductName, Quantity, Unit, Warehouse, PackagingRecipeId, ProducedByUserId, ProducedAt, Note)
OUTPUT INSERTED.ProductionRecordId
VALUES (@ProductName, @Quantity, @Unit, @Warehouse, @RecipeId, @UserId, @ProducedAt, @Note)";
            int productionId;
            await using (var productionCommand = new SqlCommand(productionSql, connection, transaction))
            {
                productionCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = model.ProductName.Trim();
                AddQuantity(productionCommand, model.Quantity);
                productionCommand.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = recipe.Unit;
                productionCommand.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = model.Warehouse;
                productionCommand.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipe.Id;
                productionCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                productionCommand.Parameters.Add("@ProducedAt", SqlDbType.DateTime2).Value = DateTime.SpecifyKind(model.ProducedAt, DateTimeKind.Local).ToUniversalTime();
                productionCommand.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(model.Note) ? DBNull.Value : model.Note.Trim();
                productionId = Convert.ToInt32(await productionCommand.ExecuteScalarAsync(cancellationToken));
            }

            const string findStockSql = @"SELECT TOP (1) StockItemId FROM dbo.StockItems WITH (UPDLOCK, HOLDLOCK)
WHERE ProductName = @ProductName AND ProductType = N'Mamul' AND Unit = @Unit
  AND Warehouse = @Warehouse AND PackagingRecipeId = @RecipeId ORDER BY StockItemId";
            int? stockItemId;
            await using (var findStock = new SqlCommand(findStockSql, connection, transaction))
            {
                findStock.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = model.ProductName.Trim();
                findStock.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = recipe.Unit;
                findStock.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = model.Warehouse;
                findStock.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipe.Id;
                var result = await findStock.ExecuteScalarAsync(cancellationToken);
                stockItemId = result is null or DBNull ? null : Convert.ToInt32(result);
            }

            if (stockItemId.HasValue)
            {
                await using var updateStock = new SqlCommand("UPDATE dbo.StockItems SET Quantity = Quantity + @Quantity WHERE StockItemId = @StockItemId", connection, transaction);
                AddQuantity(updateStock, model.Quantity);
                updateStock.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId.Value;
                await updateStock.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertStockSql = @"INSERT INTO dbo.StockItems
(ProductName, ProductType, Quantity, Unit, Warehouse, PackagingRecipeId)
OUTPUT INSERTED.StockItemId
VALUES (@ProductName, N'Mamul', @Quantity, @Unit, @Warehouse, @RecipeId)";
                await using var insertStock = new SqlCommand(insertStockSql, connection, transaction);
                insertStock.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = model.ProductName.Trim();
                AddQuantity(insertStock, model.Quantity);
                insertStock.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = recipe.Unit;
                insertStock.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = model.Warehouse;
                insertStock.Parameters.Add("@RecipeId", SqlDbType.Int).Value = recipe.Id;
                stockItemId = Convert.ToInt32(await insertStock.ExecuteScalarAsync(cancellationToken));
            }

            const string movementSql = @"INSERT INTO dbo.StockMovements
(StockItemId, ProductName, ProductType, MovementType, Quantity, Unit, Warehouse, Reason, Note, PerformedByUserId)
VALUES (@StockItemId, @ProductName, N'Mamul', N'Giriş', @Quantity, @Unit, @Warehouse, N'Üretim',
        N'Üretim no: ' + CONVERT(nvarchar(20), @ProductionId), @UserId)";
            await using (var movement = new SqlCommand(movementSql, connection, transaction))
            {
                movement.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId!.Value;
                movement.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = model.ProductName.Trim();
                AddQuantity(movement, model.Quantity);
                movement.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = recipe.Unit;
                movement.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = model.Warehouse;
                movement.Parameters.Add("@ProductionId", SqlDbType.Int).Value = productionId;
                movement.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await movement.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddQuantity(SqlCommand command, decimal quantity)
    {
        var parameter = command.Parameters.Add("@Quantity", SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = quantity;
    }
}
