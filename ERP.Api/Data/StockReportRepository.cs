using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Data;

public sealed class StockReportRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<StockReportPageViewModel> GetReportAsync(DateTime fromUtc, DateTime toUtcExclusive, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var report = new StockReportPageViewModel { FromDate = fromDate, ToDate = toDate };
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string summarySql = @"SELECT Warehouse, ProductType, Unit, SUM(Quantity), COUNT(DISTINCT ProductName)
FROM dbo.StockItems GROUP BY Warehouse, ProductType, Unit ORDER BY Warehouse, ProductType, Unit";
        await using (var command = new SqlCommand(summarySql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                report.Summary.Add(new StockReportSummaryItem
                {
                    Warehouse = reader.GetString(0), ProductType = reader.GetString(1), Unit = reader.GetString(2),
                    TotalQuantity = reader.GetDecimal(3), ProductCount = reader.GetInt32(4)
                });
        }

        const string lowStockSql = @"SELECT ProductName, ProductType, Warehouse, Unit, SUM(Quantity), MAX(MinimumQuantity)
FROM dbo.StockItems GROUP BY ProductName, ProductType, Warehouse, Unit
HAVING MAX(MinimumQuantity) > 0 AND SUM(Quantity) <= MAX(MinimumQuantity)
ORDER BY Warehouse, ProductName";
        await using (var command = new SqlCommand(lowStockSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                report.LowStock.Add(new StockLowItem
                {
                    ProductName = reader.GetString(0), ProductType = reader.GetString(1), Warehouse = reader.GetString(2),
                    Unit = reader.GetString(3), Quantity = reader.GetDecimal(4), MinimumQuantity = reader.GetDecimal(5)
                });
        }

        const string movementSql = @"SELECT m.CreatedAt, m.ProductName, m.ProductType, m.MovementType,
m.Quantity, m.Unit, m.Warehouse, m.Reason, m.Note, CONCAT(u.Ad, N' ', u.SoyAd)
FROM dbo.StockMovements m LEFT JOIN dbo.UserTable u ON u.UserId = m.PerformedByUserId
WHERE m.CreatedAt >= @FromUtc AND m.CreatedAt < @ToUtc
ORDER BY m.CreatedAt DESC, m.StockMovementId DESC";
        await using (var command = new SqlCommand(movementSql, connection))
        {
            command.Parameters.Add("@FromUtc", SqlDbType.DateTime2).Value = fromUtc;
            command.Parameters.Add("@ToUtc", SqlDbType.DateTime2).Value = toUtcExclusive;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                report.Movements.Add(new StockMovementItemViewModel
                {
                    CreatedAt = reader.GetDateTime(0), ProductName = reader.GetString(1), ProductType = reader.GetString(2),
                    MovementType = reader.GetString(3), Quantity = reader.GetDecimal(4), Unit = reader.GetString(5),
                    Warehouse = reader.GetString(6), Reason = reader.GetString(7), Note = reader.IsDBNull(8) ? null : reader.GetString(8),
                    PerformedBy = reader.IsDBNull(9) ? "Kullanıcı" : reader.GetString(9)
                });
        }

        return report;
    }
}
