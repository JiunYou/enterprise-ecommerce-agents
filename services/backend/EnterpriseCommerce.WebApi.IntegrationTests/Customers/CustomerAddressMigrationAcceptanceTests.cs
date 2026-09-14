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

namespace EnterpriseCommerce.WebApi.IntegrationTests.Customers;

public class CustomerAddressMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "customer_address_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public CustomerAddressMigrationAcceptanceTests()
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
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddCustomerAddresses"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddCustomerAddresses does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddCustomerAddresses is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    [Fact]
    public async Task Migration_AddCustomerAddresses_Lifecycle_Upgrade_Fresh_Down_Acceptance()
    {
        await using var dbContext = CreateDbContext();
        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        previousMigration.Should().Contain("AddProductReviews");
        targetMigration.Should().Contain("AddCustomerAddresses");

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // 1. 遷移至前一個最新版本 AddProductReviews
        await migrator.MigrateAsync(previousMigration);

        // 2. 應用目標遷移 AddCustomerAddresses
        await migrator.MigrateAsync(targetMigration);

        // 驗證 CustomerAddresses 資料表存在及其欄位
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        var columns = await GetTableColumnsAsync(conn, "CustomerAddresses");
        columns.Should().ContainKey("Id");
        columns.Should().ContainKey("CustomerId");
        columns.Should().ContainKey("RecipientName");
        columns.Should().ContainKey("Phone");
        columns.Should().ContainKey("CountryCode");
        columns.Should().ContainKey("PostalCode");
        columns.Should().ContainKey("City");
        columns.Should().ContainKey("AddressLine1");
        columns.Should().ContainKey("AddressLine2");
        columns.Should().ContainKey("CreatedAt");

        // 驗證 CustomerId 索引
        var hasIndex = await HasIndexAsync(conn, "CustomerAddresses", "CustomerId");
        hasIndex.Should().BeTrue("CustomerAddresses 必須具備 CustomerId 索引");

        // 驗證無唯一索引約束（地址內容無唯一約束）
        var hasUniqueIndex = await HasUniqueIndexOnColumnAsync(conn, "CustomerAddresses", "CustomerId");
        hasUniqueIndex.Should().BeFalse("CustomerId 索引不應為唯一索引");

        // 驗證無外鍵 (No FK to CustomerIdentities, Orders)
        var fkCount = await GetForeignKeyCountAsync(conn, "CustomerAddresses");
        fkCount.Should().Be(0, "CustomerAddresses 不應包含任何外鍵約束");

        // 3. 測試 Down：回復至 previousMigration，確認 CustomerAddresses 資料表已被移除
        await migrator.MigrateAsync(previousMigration);
        var tableExistsAfterDown = await TableExistsAsync(conn, "CustomerAddresses");
        tableExistsAfterDown.Should().BeFalse("Down 之後 CustomerAddresses 資料表必須被移除");

        // 4. 測試 Fresh 完整升級至最新
        await migrator.MigrateAsync();
        var tableExistsFresh = await TableExistsAsync(conn, "CustomerAddresses");
        tableExistsFresh.Should().BeTrue("Fresh 升級之後 CustomerAddresses 資料表必須存在");
    }

    private static async Task<Dictionary<string, string>> GetTableColumnsAsync(DbConnection connection, string tableName)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COLUMN_NAME, DATA_TYPE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }
        return columns;
    }

    private static async Task<bool> HasIndexAsync(DbConnection connection, string tableName, string columnName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.STATISTICS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = '{tableName}' 
              AND COLUMN_NAME = '{columnName}'";

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<bool> HasUniqueIndexOnColumnAsync(DbConnection connection, string tableName, string columnName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.STATISTICS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = '{tableName}' 
              AND COLUMN_NAME = '{columnName}'
              AND NON_UNIQUE = 0";

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<int> GetForeignKeyCountAsync(DbConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = '{tableName}' 
              AND REFERENCED_TABLE_NAME IS NOT NULL";

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = '{tableName}'";

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }
}
