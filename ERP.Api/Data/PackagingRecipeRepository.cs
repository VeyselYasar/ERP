using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class PackagingRecipeRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<List<PackagingRecipeItemViewModel>> GetRecipesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PackagingRecipeId, RecipeName, ProductType, Unit, QuantityPerPackage, PackagesPerPallet FROM dbo.PackagingRecipes ORDER BY ProductType, RecipeName";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
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

    public async Task AddRecipeAsync(PackagingRecipePageViewModel recipe, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE dbo.PackagingRecipes
SET ProductType = @ProductType, Unit = @Unit, QuantityPerPackage = @QuantityPerPackage,
    PackagesPerPallet = @PackagesPerPallet
WHERE RecipeName = @RecipeName AND ProductType = @ProductType AND Unit = @Unit;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.PackagingRecipes (RecipeName, ProductType, Unit, QuantityPerPackage, PackagesPerPallet)
    VALUES (@RecipeName, @ProductType, @Unit, @QuantityPerPackage, @PackagesPerPallet);";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@RecipeName", SqlDbType.NVarChar, 100).Value = recipe.RecipeName.Trim();
        command.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = recipe.ProductType;
        command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = recipe.Unit;
        var quantity = command.Parameters.Add("@QuantityPerPackage", SqlDbType.Decimal);
        quantity.Precision = 18;
        quantity.Scale = 3;
        quantity.Value = recipe.QuantityPerPackage;
        command.Parameters.Add("@PackagesPerPallet", SqlDbType.Int).Value = recipe.PackagesPerPallet;
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
