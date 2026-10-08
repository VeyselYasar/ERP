using System.Data;
using ERP.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Rendering;
using ERP.Api.Security;

namespace ERP.Api.Data;

public sealed class AdminRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection ayarı bulunamadı.");

    public async Task<bool> IsAdminEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.WhoIsAdmin WHERE LTRIM(RTRIM(Eposta)) = @Email) THEN 1 ELSE 0 END";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
        await connection.OpenAsync(cancellationToken);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<AdminPermissionsViewModel> GetPermissionsPageAsync(
        int? requestedUserId,
        CancellationToken cancellationToken = default)
    {
        var model = new AdminPermissionsViewModel();
        var users = new List<(int Id, string Name, string Email)>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string usersSql = "SELECT UserId, Ad, SoyAd, Mail FROM dbo.UserTable WHERE AktifPasif = 1 ORDER BY Ad, SoyAd";
        await using (var command = new SqlCommand(usersSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                users.Add((reader.GetInt32(0), $"{reader.GetString(1)} {reader.GetString(2)}", reader.GetString(3)));
        }

        if (users.Count == 0) return model;

        model.SelectedUserId = users.Any(user => user.Id == requestedUserId)
            ? requestedUserId!.Value
            : users[0].Id;
        model.Users = users.Select(user => new SelectListItem
        {
            Value = user.Id.ToString(),
            Text = $"{user.Name} ({user.Email})",
            Selected = user.Id == model.SelectedUserId
        }).ToList();

        const string permissionSql = @"
SELECT t.TableName,
       ISNULL(p.Okuma, CAST(0 AS bit)) AS Okuma,
       ISNULL(p.Yazma, CAST(0 AS bit)) AS Yazma,
       ISNULL(p.Silme, CAST(0 AS bit)) AS Silme,
       ISNULL(p.Onizleme, CAST(0 AS bit)) AS Onizleme
FROM dbo.[Tables] t
LEFT JOIN dbo.KullaniciYetkiTable p
  ON p.TableName = t.TableName AND p.UserId = @UserId
WHERE t.AktifPasif = 1
  AND t.TableName NOT IN (N'test1', N'test2', N'test3')
ORDER BY CASE t.TableName WHEN N'STOK' THEN 0 WHEN N'STOK RAPORU' THEN 1 WHEN N'AMBALAJ REÇETESİ' THEN 2 WHEN N'ÜRETİM KAYDI' THEN 3 WHEN N'DEPO TRANSFERİ' THEN 4 WHEN N'STOK HAREKETLERİ' THEN 5 WHEN N'MÜŞTERİLER' THEN 6 WHEN N'SATIŞ' THEN 7 WHEN N'SATIŞ RAPORU' THEN 8 ELSE 9 END, t.TableName";
        await using var permissionCommand = new SqlCommand(permissionSql, connection);
        permissionCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = model.SelectedUserId;
        await using var permissionReader = await permissionCommand.ExecuteReaderAsync(cancellationToken);
        while (await permissionReader.ReadAsync(cancellationToken))
        {
            model.Permissions.Add(new TablePermissionViewModel
            {
                TableName = permissionReader.GetString(0),
                Okuma = permissionReader.GetBoolean(1),
                Yazma = permissionReader.GetBoolean(2),
                Silme = permissionReader.GetBoolean(3),
                Onizleme = permissionReader.GetBoolean(4)
            });
        }

        return model;
    }

    public async Task SavePermissionsAsync(
        int userId,
        IReadOnlyCollection<TablePermissionViewModel> permissions,
        CancellationToken cancellationToken = default)
    {
        const string updateSql = @"
UPDATE dbo.KullaniciYetkiTable
SET Okuma = @Okuma, Yazma = @Yazma, Silme = @Silme, Onizleme = @Onizleme
WHERE UserId = @UserId AND TableName = @TableName";
        const string insertSql = @"
INSERT INTO dbo.KullaniciYetkiTable (UserId, TableName, Okuma, Yazma, Silme, Onizleme)
VALUES (@UserId, @TableName, @Okuma, @Yazma, @Silme, @Onizleme)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var permission in permissions)
            {
                await using var command = new SqlCommand(updateSql, connection, transaction);
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                command.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = permission.TableName;
                command.Parameters.Add("@Okuma", SqlDbType.Bit).Value = permission.Okuma;
                command.Parameters.Add("@Yazma", SqlDbType.Bit).Value = permission.Yazma;
                command.Parameters.Add("@Silme", SqlDbType.Bit).Value = permission.Silme;
                command.Parameters.Add("@Onizleme", SqlDbType.Bit).Value = permission.Onizleme;

                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    command.CommandText = insertSql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> HasPermissionAsync(
        int userId,
        string tableName,
        TablePermissionKind permission,
        CancellationToken cancellationToken = default)
    {
        var columnName = permission switch
        {
            TablePermissionKind.Okuma => "Okuma",
            TablePermissionKind.Yazma => "Yazma",
            TablePermissionKind.Silme => "Silme",
            TablePermissionKind.Onizleme => "Onizleme",
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        var sql = $@"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.KullaniciYetkiTable p
    INNER JOIN dbo.[Tables] t ON t.TableName = p.TableName AND t.AktifPasif = 1
    WHERE p.UserId = @UserId AND p.TableName = @TableName AND p.{columnName} = 1
) THEN 1 ELSE 0 END";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = tableName;
        await connection.OpenAsync(cancellationToken);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<List<string>> GetVisibleTablesAsync(
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT t.TableName
FROM dbo.[Tables] t
WHERE t.AktifPasif = 1
  AND t.TableName NOT IN (N'test1', N'test2', N'test3')
  AND
  (
      @IsAdmin = 1
      OR EXISTS
      (
          SELECT 1
          FROM dbo.KullaniciYetkiTable p
          WHERE p.UserId = @UserId
            AND p.TableName = t.TableName
            AND (p.Okuma = 1 OR p.Yazma = 1 OR p.Silme = 1 OR p.Onizleme = 1)
      )
  )
ORDER BY CASE t.TableName WHEN N'STOK' THEN 0 WHEN N'STOK RAPORU' THEN 1 WHEN N'AMBALAJ REÇETESİ' THEN 2 WHEN N'ÜRETİM KAYDI' THEN 3 WHEN N'DEPO TRANSFERİ' THEN 4 WHEN N'STOK HAREKETLERİ' THEN 5 WHEN N'MÜŞTERİLER' THEN 6 WHEN N'SATIŞ' THEN 7 WHEN N'SATIŞ RAPORU' THEN 8 ELSE 9 END, t.TableName";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = isAdmin;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(reader.GetString(0));
        return tables;
    }

    public async Task<TableAccessViewModel?> GetTableAccessAsync(
        int userId,
        bool isAdmin,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT t.TableName,
       ISNULL(p.Okuma, CAST(0 AS bit)) AS Okuma,
       ISNULL(p.Yazma, CAST(0 AS bit)) AS Yazma,
       ISNULL(p.Silme, CAST(0 AS bit)) AS Silme,
       ISNULL(p.Onizleme, CAST(0 AS bit)) AS Onizleme
FROM dbo.[Tables] t
LEFT JOIN dbo.KullaniciYetkiTable p
  ON p.TableName = t.TableName AND p.UserId = @UserId
WHERE t.TableName = @TableName AND t.AktifPasif = 1
  AND t.TableName NOT IN (N'test1', N'test2', N'test3')";
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = tableName;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new TableAccessViewModel
        {
            TableName = reader.GetString(0),
            Okuma = isAdmin || reader.GetBoolean(1),
            Yazma = isAdmin || reader.GetBoolean(2),
            Silme = isAdmin || reader.GetBoolean(3),
            Onizleme = isAdmin || reader.GetBoolean(4),
            IsAdmin = isAdmin
        };
    }
}
