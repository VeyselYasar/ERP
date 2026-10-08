using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class StockRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<List<StockItemViewModel>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT s.StockItemId, s.ProductName, s.ProductType, s.Quantity, s.MinimumQuantity, s.Unit, s.Warehouse, r.RecipeName
FROM dbo.StockItems s LEFT JOIN dbo.PackagingRecipes r ON r.PackagingRecipeId = s.PackagingRecipeId
ORDER BY s.ProductName, s.StockItemId";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<StockItemViewModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StockItemViewModel
            {
                Id = reader.GetInt32(0), ProductName = reader.GetString(1), ProductType = reader.GetString(2),
                Quantity = reader.GetDecimal(3), MinimumQuantity = reader.GetDecimal(4), Unit = reader.GetString(5), Warehouse = reader.GetString(6),
                PackagingRecipeName = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return items;
    }

    public async Task AddItemAsync(StockPageViewModel item, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"INSERT INTO dbo.StockItems (ProductName, ProductType, Quantity, MinimumQuantity, Unit, Warehouse, PackagingRecipeId)
OUTPUT INSERTED.StockItemId
VALUES (@ProductName, @ProductType, @Quantity, @MinimumQuantity, @Unit, @Warehouse, @PackagingRecipeId)";
            int stockItemId;
            await using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = item.ProductName.Trim();
                command.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = item.ProductType.Trim();
                AddAmount(command, item.Quantity);
                AddMinimum(command, item.MinimumQuantity);
                command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = item.Unit;
                command.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = item.Warehouse;
                command.Parameters.Add("@PackagingRecipeId", SqlDbType.Int).Value = item.PackagingRecipeId.HasValue ? item.PackagingRecipeId.Value : DBNull.Value;
                stockItemId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }

            const string movementSql = @"INSERT INTO dbo.StockMovements
(StockItemId, ProductName, ProductType, MovementType, Quantity, Unit, Warehouse, Reason, Note, PerformedByUserId)
VALUES (@StockItemId, @ProductName, @ProductType, N'Giriş', @Quantity, @Unit, @Warehouse, N'İlk stok girişi', N'Yeni stok kartı oluşturuldu', @UserId)";
            await using (var movement = new SqlCommand(movementSql, connection, transaction))
            {
                movement.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
                movement.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = item.ProductName.Trim();
                movement.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = item.ProductType.Trim();
                AddAmount(movement, item.Quantity);
                movement.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = item.Unit;
                movement.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = item.Warehouse;
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

    public async Task UpdateMinimumQuantityAsync(int stockItemId, decimal minimumQuantity, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.StockItems SET MinimumQuantity = @MinimumQuantity WHERE StockItemId = @StockItemId";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        AddMinimum(command, minimumQuantity);
        command.Parameters.Add("@StockItemId", SqlDbType.Int).Value = stockItemId;
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<PackagingRecipeItemViewModel>> GetRecipesForStockAsync(string productType, string unit, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT PackagingRecipeId, RecipeName, ProductType, Unit, QuantityPerPackage, PackagesPerPallet
FROM dbo.PackagingRecipes WHERE ProductType = @ProductType AND Unit = @Unit ORDER BY RecipeName";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
        command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var recipes = new List<PackagingRecipeItemViewModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var quantityPerPackage = reader.GetDecimal(4);
            var packagesPerPallet = reader.GetInt32(5);
            recipes.Add(new PackagingRecipeItemViewModel
            {
                Id = reader.GetInt32(0), RecipeName = reader.GetString(1), ProductType = reader.GetString(2),
                Unit = reader.GetString(3), QuantityPerPackage = quantityPerPackage,
                PackagesPerPallet = packagesPerPallet, QuantityPerPallet = quantityPerPackage * packagesPerPallet
            });
        }
        return recipes;
    }

    private static void AddAmount(SqlCommand command, decimal quantity)
    {
        var parameter = command.Parameters.Add("@Quantity", SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = quantity;
    }

    private static void AddMinimum(SqlCommand command, decimal quantity)
    {
        var parameter = command.Parameters.Add("@MinimumQuantity", SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = quantity;
    }
}
