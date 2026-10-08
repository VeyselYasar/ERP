using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class SalesRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<SalePageViewModel> GetPageAsync(bool canRead, bool canWrite, CancellationToken cancellationToken = default)
    {
        var page = new SalePageViewModel { CanRead = canRead, CanWrite = canWrite };
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (canWrite)
        {
            const string customersSql = @"SELECT CustomerId, CustomerCode, Title
FROM dbo.Customers WHERE IsActive = 1 ORDER BY Title";
            await using (var command = new SqlCommand(customersSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    page.Customers.Add(new SaleCustomerOption
                    {
                        CustomerId = reader.GetInt32(0), CustomerCode = reader.GetString(1), Title = reader.GetString(2)
                    });
            }

            const string stockSql = @"SELECT StockItemId, ProductName, ProductType, Quantity, Unit, Warehouse
FROM dbo.StockItems WHERE Quantity > 0 ORDER BY ProductName, Warehouse, StockItemId";
            await using (var command = new SqlCommand(stockSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    page.StockItems.Add(new SaleStockOption
                    {
                        StockItemId = reader.GetInt32(0), ProductName = reader.GetString(1),
                        ProductType = reader.GetString(2), Quantity = reader.GetDecimal(3),
                        Unit = reader.GetString(4), Warehouse = reader.GetString(5)
                    });
            }
        }

        if (canRead)
        {
            const string historySql = @"SELECT TOP (200) s.SaleId, s.SaleDate, c.Title, s.ProductName,
s.ProductType, s.Quantity, s.Unit, s.Warehouse, s.UnitPrice, s.TotalAmount,
s.ShipmentType, s.Note, CONCAT(u.Ad, N' ', u.SoyAd)
FROM dbo.Sales s
INNER JOIN dbo.Customers c ON c.CustomerId = s.CustomerId
LEFT JOIN dbo.UserTable u ON u.UserId = s.CreatedByUserId
ORDER BY s.SaleDate DESC, s.SaleId DESC";
            await using var command = new SqlCommand(historySql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                page.Sales.Add(new SaleHistoryItem
                {
                    SaleId = reader.GetInt32(0), SaleDate = reader.GetDateTime(1), CustomerTitle = reader.GetString(2),
                    ProductName = reader.GetString(3), ProductType = reader.GetString(4), Quantity = reader.GetDecimal(5),
                    Unit = reader.GetString(6), Warehouse = reader.GetString(7), UnitPrice = reader.GetDecimal(8),
                    TotalAmount = reader.GetDecimal(9), ShipmentType = reader.GetString(10),
                    Note = reader.IsDBNull(11) ? null : reader.GetString(11),
                    CreatedBy = reader.IsDBNull(12) ? "Kullanıcı" : reader.GetString(12)
                });
        }

        return page;
    }

    public async Task<SalesReportPageViewModel> GetReportAsync(
        DateTime? fromDate,
        DateTime? toDateExclusive,
        int? customerId,
        string? productName,
        string? shipmentType,
        CancellationToken cancellationToken = default)
    {
        var report = new SalesReportPageViewModel
        {
            FromDate = fromDate,
            ToDate = toDateExclusive?.AddDays(-1),
            CustomerId = customerId,
            ProductName = productName,
            ShipmentType = shipmentType
        };
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string customersSql = @"SELECT CustomerId, CustomerCode, Title FROM dbo.Customers ORDER BY Title";
        await using (var command = new SqlCommand(customersSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                report.Customers.Add(new SaleCustomerOption
                {
                    CustomerId = reader.GetInt32(0), CustomerCode = reader.GetString(1), Title = reader.GetString(2)
                });
        }

        const string salesSql = @"SELECT s.SaleId, s.SaleDate, c.Title, s.ProductName,
s.ProductType, s.Quantity, s.Unit, s.Warehouse, s.UnitPrice, s.TotalAmount,
s.ShipmentType, s.Note, CONCAT(u.Ad, N' ', u.SoyAd)
FROM dbo.Sales s
INNER JOIN dbo.Customers c ON c.CustomerId = s.CustomerId
LEFT JOIN dbo.UserTable u ON u.UserId = s.CreatedByUserId
WHERE (@FromDate IS NULL OR s.SaleDate >= @FromDate)
  AND (@ToDateExclusive IS NULL OR s.SaleDate < @ToDateExclusive)
  AND (@CustomerId IS NULL OR s.CustomerId = @CustomerId)
  AND (@ProductName IS NULL OR s.ProductName LIKE @ProductName)
  AND (@ShipmentType IS NULL OR s.ShipmentType = @ShipmentType)
ORDER BY s.SaleDate DESC, s.SaleId DESC";
        await using (var command = new SqlCommand(salesSql, connection))
        {
            command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value;
            command.Parameters.Add("@ToDateExclusive", SqlDbType.DateTime2).Value = toDateExclusive.HasValue ? (object)toDateExclusive.Value.Date : DBNull.Value;
            command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId.HasValue ? (object)customerId.Value : DBNull.Value;
            command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(productName)
                ? DBNull.Value
                : $"%{productName.Trim()}%";
            command.Parameters.Add("@ShipmentType", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(shipmentType)
                ? DBNull.Value
                : shipmentType;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sale = new SaleHistoryItem
                {
                    SaleId = reader.GetInt32(0), SaleDate = reader.GetDateTime(1), CustomerTitle = reader.GetString(2),
                    ProductName = reader.GetString(3), ProductType = reader.GetString(4), Quantity = reader.GetDecimal(5),
                    Unit = reader.GetString(6), Warehouse = reader.GetString(7), UnitPrice = reader.GetDecimal(8),
                    TotalAmount = reader.GetDecimal(9), ShipmentType = reader.GetString(10),
                    Note = reader.IsDBNull(11) ? null : reader.GetString(11),
                    CreatedBy = reader.IsDBNull(12) ? "Kullanıcı" : reader.GetString(12)
                };
                report.TotalAmount += sale.TotalAmount;
                report.Sales.Add(sale);
            }
        }

        return report;
    }

    public async Task<string?> RecordAsync(SalePageViewModel sale, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string sourceSql = @"SELECT s.ProductName, s.ProductType, s.Quantity, s.Unit, s.Warehouse
FROM dbo.StockItems s WITH (UPDLOCK, HOLDLOCK)
WHERE s.StockItemId = @StockItemId";
            string productName;
            string productType;
            decimal availableQuantity;
            string unit;
            string warehouse;
            await using (var command = new SqlCommand(sourceSql, connection, transaction))
            {
                command.Parameters.Add("@StockItemId", SqlDbType.Int).Value = sale.StockItemId!.Value;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return "Seçilen ürün artık stokta bulunmuyor.";
                productName = reader.GetString(0);
                productType = reader.GetString(1);
                availableQuantity = reader.GetDecimal(2);
                unit = reader.GetString(3);
                warehouse = reader.GetString(4);
            }

            if (sale.Quantity > availableQuantity)
                return $"Seçilen depoda yalnızca {availableQuantity:0.###} {unit} stok var.";

            const string customerSql = @"SELECT Title FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
WHERE CustomerId = @CustomerId AND IsActive = 1";
            string customerTitle;
            await using (var command = new SqlCommand(customerSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = sale.CustomerId!.Value;
                var title = await command.ExecuteScalarAsync(cancellationToken);
                if (title is null or DBNull) return "Müşteri bulunamadı veya pasif durumda.";
                customerTitle = (string)title;
            }

            var totalAmount = decimal.Round(sale.Quantity * sale.UnitPrice, 2, MidpointRounding.AwayFromZero);
            const string insertSaleSql = @"INSERT INTO dbo.Sales
(CustomerId, ProductName, ProductType, Quantity, Unit, Warehouse, UnitPrice, TotalAmount, ShipmentType, SaleDate, Note, CreatedByUserId)
OUTPUT INSERTED.SaleId
VALUES (@CustomerId, @ProductName, @ProductType, @Quantity, @Unit, @Warehouse, @UnitPrice, @TotalAmount, @ShipmentType, @SaleDate, @Note, @UserId)";
            int saleId;
            await using (var command = new SqlCommand(insertSaleSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = sale.CustomerId!.Value;
                command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                command.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                AddDecimal(command, "@Quantity", sale.Quantity, 3);
                command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                command.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = warehouse;
                AddDecimal(command, "@UnitPrice", sale.UnitPrice, 4);
                AddDecimal(command, "@TotalAmount", totalAmount, 2);
                command.Parameters.Add("@ShipmentType", SqlDbType.NVarChar, 50).Value = sale.ShipmentType;
                command.Parameters.Add("@SaleDate", SqlDbType.DateTime2).Value = sale.SaleDate.Date;
                command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(sale.Note) ? DBNull.Value : sale.Note.Trim();
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                saleId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }

            if (sale.Quantity == availableQuantity)
            {
                await using var delete = new SqlCommand("DELETE FROM dbo.StockItems WHERE StockItemId = @StockItemId", connection, transaction);
                delete.Parameters.Add("@StockItemId", SqlDbType.Int).Value = sale.StockItemId!.Value;
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string updateSql = @"UPDATE dbo.StockItems
SET Quantity = Quantity - @Quantity WHERE StockItemId = @StockItemId AND Quantity >= @Quantity";
                await using var update = new SqlCommand(updateSql, connection, transaction);
                AddDecimal(update, "@Quantity", sale.Quantity, 3);
                update.Parameters.Add("@StockItemId", SqlDbType.Int).Value = sale.StockItemId!.Value;
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("Stok miktarı satış sırasında değişti.");
            }

            var movementNote = $"Satış #{saleId} · {customerTitle}";
            const string movementSql = @"INSERT INTO dbo.StockMovements
(StockItemId, ProductName, ProductType, MovementType, Quantity, Unit, Warehouse, Reason, Note, PerformedByUserId)
VALUES (@StockItemId, @ProductName, @ProductType, N'Çıkış', @Quantity, @Unit, @Warehouse, N'Satış / sevk', @Note, @UserId)";
            await using (var command = new SqlCommand(movementSql, connection, transaction))
            {
                command.Parameters.Add("@StockItemId", SqlDbType.Int).Value = sale.StockItemId!.Value;
                command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 150).Value = productName;
                command.Parameters.Add("@ProductType", SqlDbType.NVarChar, 30).Value = productType;
                AddDecimal(command, "@Quantity", sale.Quantity, 3);
                command.Parameters.Add("@Unit", SqlDbType.NVarChar, 20).Value = unit;
                command.Parameters.Add("@Warehouse", SqlDbType.NVarChar, 30).Value = warehouse;
                command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = movementNote.Length <= 300 ? movementNote : movementNote[..300];
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte scale)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = scale;
        parameter.Value = value;
    }
}
