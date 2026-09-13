using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

public class ProductReviewMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "product_review_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public ProductReviewMigrationAcceptanceTests()
    {
        _mySqlContainer = new MySqlBuilder("mysql:8.0")
            .WithDatabase(DatabaseName)
            .WithUsername("test")
            .WithPassword("test")
            .WithCommand("--max_connections=1000", "--max_connect_errors=10000")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _mySqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mySqlContainer.DisposeAsync();
    }

    private EnterpriseCommerceDbContext CreateDbContext()
    {
        var connStr = _mySqlContainer.GetConnectionString() + ";Max Pool Size=200;";
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(connStr, ServerVersion.AutoDetect(connStr))
            .Options;

        return new EnterpriseCommerceDbContext(options);
    }

    private static (string PreviousMigration, string TargetMigration) ResolveMigrations(EnterpriseCommerceDbContext dbContext)
    {
        var allMigrations = dbContext.Database.GetMigrations().ToList();
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddProductReviews"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddProductReviews does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddProductReviews is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    [Fact]
    public async Task Migration_AddProductReviews_Lifecycle_Upgrade_Fresh_Down_Acceptance()
    {
        await using var dbContext = CreateDbContext();
        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        previousMigration.Should().Contain("AddWishlistItems");
        targetMigration.Should().Contain("AddProductReviews");

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // 1. 遷移至前一個最新版本 AddWishlistItems
        await migrator.MigrateAsync(previousMigration);

        // 2. 應用目標遷移 AddProductReviews
        await migrator.MigrateAsync(targetMigration);

        // 驗證 ProductReviews 資料表存在及其欄位
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        var columns = await GetTableColumnsAsync(conn, "ProductReviews");
        columns.Should().ContainKey("Id");
        columns.Should().ContainKey("CustomerId");
        columns.Should().ContainKey("ProductId");
        columns.Should().ContainKey("Rating");
        columns.Should().ContainKey("Comment");
        columns.Should().ContainKey("CreatedAt");

        // 驗證唯一索引 UNIQUE(CustomerId, ProductId)
        var hasUniqueIndex = await HasUniqueIndexAsync(conn, "ProductReviews", "CustomerId", "ProductId");
        hasUniqueIndex.Should().BeTrue("ProductReviews 必須具備 CustomerId 與 ProductId 的複合唯一索引");

        // 驗證無外鍵 (No FK to Product, Customer, Order)
        var fkCount = await GetForeignKeyCountAsync(conn, "ProductReviews");
        fkCount.Should().Be(0, "ProductReviews 不應包含任何外鍵約束");

        // 3. 測試 Down：回復至 previousMigration，確認 ProductReviews 資料表已被移除
        await migrator.MigrateAsync(previousMigration);
        var tableExistsAfterDown = await TableExistsAsync(conn, "ProductReviews");
        tableExistsAfterDown.Should().BeFalse();

        // 4. 測試 Fresh 完整升級至最新
        await migrator.MigrateAsync();
        var tableExistsAfterFresh = await TableExistsAsync(conn, "ProductReviews");
        tableExistsAfterFresh.Should().BeTrue();
    }

    private static async Task<Dictionary<string, string>> GetTableColumnsAsync(DbConnection connection, string tableName)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COLUMN_NAME, DATA_TYPE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}';";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }
        return columns;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}';";
        var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return result > 0;
    }

    private static async Task<bool> HasUniqueIndexAsync(DbConnection connection, string tableName, string col1, string col2)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT INDEX_NAME, COLUMN_NAME, NON_UNIQUE
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'
            ORDER BY INDEX_NAME, SEQ_IN_INDEX;";

        var indexColumns = new Dictionary<string, (bool NonUnique, List<string> Columns)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var idxName = reader.GetString(0);
            var colName = reader.GetString(1);
            var nonUnique = reader.GetInt32(2) == 1;

            if (!indexColumns.ContainsKey(idxName))
            {
                indexColumns[idxName] = (nonUnique, new List<string>());
            }
            indexColumns[idxName].Columns.Add(colName);
        }

        return indexColumns.Values.Any(v =>
            !v.NonUnique &&
            v.Columns.Count == 2 &&
            v.Columns.Contains(col1, StringComparer.OrdinalIgnoreCase) &&
            v.Columns.Contains(col2, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<int> GetForeignKeyCountAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = '{tableName}' 
              AND REFERENCED_TABLE_NAME IS NOT NULL;";
        var result = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return result;
    }
}
