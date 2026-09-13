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

public class WishlistMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "wishlist_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public WishlistMigrationAcceptanceTests()
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
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddWishlistItems"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddWishlistItems does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddWishlistItems is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    [Fact]
    public async Task Migration_AddWishlistItems_Lifecycle_Upgrade_Fresh_Down_Acceptance()
    {
        await using var dbContext = CreateDbContext();
        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        previousMigration.Should().Contain("AddProductCategory");
        targetMigration.Should().Contain("AddWishlistItems");

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // 1. 遷移至前一個最新版本 AddProductCategory
        await migrator.MigrateAsync(previousMigration);

        // 2. 應用目標遷移 AddWishlistItems
        await migrator.MigrateAsync(targetMigration);

        // 驗證 WishlistItems 資料表存在及其欄位
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        var columns = await GetTableColumnsAsync(conn, "WishlistItems");
        columns.Should().ContainKey("Id");
        columns.Should().ContainKey("CustomerId");
        columns.Should().ContainKey("ProductId");
        columns.Should().ContainKey("AddedAt");

        // 3. 測試 Down：回復至 previousMigration，確認 WishlistItems 資料表已被移除
        await migrator.MigrateAsync(previousMigration);
        var tableExistsAfterDown = await TableExistsAsync(conn, "WishlistItems");
        tableExistsAfterDown.Should().BeFalse();

        // 4. 測試 Fresh 完整升級至最新
        await migrator.MigrateAsync();
        var tableExistsAfterFresh = await TableExistsAsync(conn, "WishlistItems");
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
}
