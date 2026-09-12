using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

public class ProductImageUrlMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "product_image_url_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public ProductImageUrlMigrationAcceptanceTests()
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

    private static async Task ResetDatabaseAsync(EnterpriseCommerceDbContext dbContext)
    {
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS AdminOrderCancellations;
            DROP TABLE IF EXISTS PaymentRefunds;
            DROP TABLE IF EXISTS OrderItems;
            DROP TABLE IF EXISTS PaymentAttempts;
            DROP TABLE IF EXISTS PaymentWebhookReceipts;
            DROP TABLE IF EXISTS CustomerIdentities;
            DROP TABLE IF EXISTS InventoryReservations;
            DROP TABLE IF EXISTS InventoryItems;
            DROP TABLE IF EXISTS Products;
            DROP TABLE IF EXISTS OutboxMessages;
            DROP TABLE IF EXISTS Orders;
            DROP TABLE IF EXISTS `__EFMigrationsHistory`;
            SET FOREIGN_KEY_CHECKS = 1;";
        await cmd.ExecuteNonQueryAsync();
    }

    private static (string PreviousMigration, string TargetMigration) ResolveMigrations(EnterpriseCommerceDbContext dbContext)
    {
        var allMigrations = dbContext.Database.GetMigrations().ToList();
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddProductImageUrl"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddProductImageUrl does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddProductImageUrl is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    private static async Task<Dictionary<string, (string DataType, long? MaxLen, string IsNullable)>> GetProductColumnsAsync(
        DbConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'Products';";

        var p = cmd.CreateParameter();
        p.ParameterName = "@dbName";
        p.Value = DatabaseName;
        cmd.Parameters.Add(p);

        var result = new Dictionary<string, (string DataType, long? MaxLen, string IsNullable)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            long? maxLen = reader.IsDBNull(2) ? null : reader.GetInt64(2);
            var isNullable = reader.GetString(3);
            result[name] = (dataType, maxLen, isNullable);
        }

        return result;
    }

    [Fact]
    public async Task MIGRATION_UPGRADE_PREVIOUS_TO_PRODUCT_IMAGE_URL_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        // 1. 套用至上一遷移 (AddProductDescription)
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(previousMigration);

        var conn = dbContext.Database.GetDbConnection();
        var colsBefore = await GetProductColumnsAsync(conn);
        colsBefore.Should().NotContainKey("ImageUrl");
        colsBefore.Should().ContainKey("Description");

        // 2. 在舊 schema 下插入商品列 (包含 Description)
        var productId = Guid.NewGuid();
        var productName = "Historical Product";
        var productSku = "HIST-SKU-001";
        var productPrice = 99.99m;
        var productCurrency = "USD";
        var productDescription = "Historical Description";

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO Products (Id, Name, Sku, Price, Currency, IsActive, Description, Version)
                VALUES (@id, @name, @sku, @price, @currency, 1, @description, 1);";

            var pId = insertCmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = productId.ToString();
            insertCmd.Parameters.Add(pId);

            var pName = insertCmd.CreateParameter();
            pName.ParameterName = "@name";
            pName.Value = productName;
            insertCmd.Parameters.Add(pName);

            var pSku = insertCmd.CreateParameter();
            pSku.ParameterName = "@sku";
            pSku.Value = productSku;
            insertCmd.Parameters.Add(pSku);

            var pPrice = insertCmd.CreateParameter();
            pPrice.ParameterName = "@price";
            pPrice.Value = productPrice;
            insertCmd.Parameters.Add(pPrice);

            var pCurrency = insertCmd.CreateParameter();
            pCurrency.ParameterName = "@currency";
            pCurrency.Value = productCurrency;
            insertCmd.Parameters.Add(pCurrency);

            var pDesc = insertCmd.CreateParameter();
            pDesc.ParameterName = "@description";
            pDesc.Value = productDescription;
            insertCmd.Parameters.Add(pDesc);

            await insertCmd.ExecuteNonQueryAsync();
        }

        // 3. 套用目標遷移 AddProductImageUrl
        await migrator.MigrateAsync(targetMigration);

        var colsAfter = await GetProductColumnsAsync(conn);
        colsAfter.Should().ContainKey("ImageUrl");
        colsAfter["ImageUrl"].IsNullable.Should().Be("NO");
        colsAfter["ImageUrl"].MaxLen.Should().Be(2048);

        // 4. 驗證既有商品存活且 ImageUrl 為預設空字串，其餘欄位不變
        await using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = "SELECT Id, Name, Sku, Price, Currency, IsActive, Description, ImageUrl FROM Products WHERE Id = @id;";
            var pId = queryCmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = productId.ToString();
            queryCmd.Parameters.Add(pId);

            await using var reader = await queryCmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(1).Should().Be(productName);
            reader.GetString(2).Should().Be(productSku);
            reader.GetDecimal(3).Should().Be(productPrice);
            reader.GetString(4).Should().Be(productCurrency);
            reader.GetBoolean(5).Should().BeTrue();
            reader.GetString(6).Should().Be(productDescription);
            reader.GetString(7).Should().Be(string.Empty);
        }

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(targetMigration);
    }

    [Fact]
    public async Task MIGRATION_FRESH_DATABASE_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (_, targetMigration) = ResolveMigrations(dbContext);

        await dbContext.Database.MigrateAsync();

        var conn = dbContext.Database.GetDbConnection();
        var cols = await GetProductColumnsAsync(conn);

        cols.Should().ContainKey("ImageUrl");
        cols["ImageUrl"].IsNullable.Should().Be("NO");
        cols["ImageUrl"].MaxLen.Should().Be(2048);

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(targetMigration);
    }

    [Fact]
    public async Task MIGRATION_DOWN_PRODUCT_IMAGE_URL_TO_PREVIOUS_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);

        var conn = dbContext.Database.GetDbConnection();
        var colsBeforeDown = await GetProductColumnsAsync(conn);
        colsBeforeDown.Should().ContainKey("ImageUrl");

        // 降級至上一遷移
        await migrator.MigrateAsync(previousMigration);

        var colsAfterDown = await GetProductColumnsAsync(conn);
        colsAfterDown.Should().NotContainKey("ImageUrl");

        // Products 資料表與其餘欄位維持完好
        colsAfterDown.Should().ContainKey("Id");
        colsAfterDown.Should().ContainKey("Name");
        colsAfterDown.Should().ContainKey("Sku");
        colsAfterDown.Should().ContainKey("Price");
        colsAfterDown.Should().ContainKey("Currency");
        colsAfterDown.Should().ContainKey("IsActive");
        colsAfterDown.Should().ContainKey("Description");
        colsAfterDown.Should().ContainKey("Version");
    }
}
