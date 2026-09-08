using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Inventory;

public class EnforceUniqueInventoryProductReferenceMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "inventory_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public EnforceUniqueInventoryProductReferenceMigrationAcceptanceTests()
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

    [Fact]
    public async Task Migration_EnforceUniqueInventoryProductReference_AppliesSuccessfully_AndEnforcesConstraint()
    {
        // 1. 驗證遷移成功套用至乾淨資料庫
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("EnforceUniqueInventoryProductReference"));

        // 2. 驗證 InventoryItems.ProductReference 擁有 UNIQUE 索引
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @dbName
              AND TABLE_NAME = 'InventoryItems'
              AND COLUMN_NAME = 'ProductReference'
              AND NON_UNIQUE = 0";

        var p1 = command.CreateParameter();
        p1.ParameterName = "@dbName";
        p1.Value = DatabaseName;
        command.Parameters.Add(p1);

        await dbContext.Database.OpenConnectionAsync();
        var uniqueIndexes = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                uniqueIndexes.Add(reader.GetString(0));
            }
        }

        uniqueIndexes.Should().Contain("IX_InventoryItems_ProductReference",
            "InventoryItems.ProductReference 必須具有唯一索引 (INVENTORY_PRODUCT_REFERENCE_UNIQUE_INDEX=PASS)");

        // 3. 驗證第一筆 InventoryItem (ProductReference X) 可成功持久化
        var productRef = new ProductReference(Guid.NewGuid());
        var item1 = InventoryItem.Create(productRef);
        dbContext.InventoryItems.Add(item1);
        await dbContext.SaveChangesAsync();

        // 4. 驗證相同 ProductReference X 的第二筆 InventoryItem 遭 MySQL/EF Core 拒絕持久化
        var item2 = InventoryItem.Create(productRef);
        dbContext.InventoryItems.Add(item2);

        var saveAction = async () => await dbContext.SaveChangesAsync();
        var ex = await saveAction.Should().ThrowAsync<DbUpdateException>(
            "重複的 ProductReference 必須被資料庫拒絕 (DUPLICATE_PRODUCT_REFERENCE_DATABASE_REJECTED=PASS)");
        ex.Which.InnerException.Should().NotBeNull();
        ex.Which.InnerException!.Message.Should().Contain("Duplicate entry");

        // 5. 驗證未引入多餘外鍵 (絕不新增對 Products 的外鍵)
        await using var fkCommand = dbContext.Database.GetDbConnection().CreateCommand();
        fkCommand.CommandText = @"
            SELECT REFERENCED_TABLE_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @dbName
              AND TABLE_NAME = 'InventoryItems'
              AND COLUMN_NAME = 'ProductReference'
              AND REFERENCED_TABLE_NAME IS NOT NULL";

        var pFk = fkCommand.CreateParameter();
        pFk.ParameterName = "@dbName";
        pFk.Value = DatabaseName;
        fkCommand.Parameters.Add(pFk);

        var referencedTable = await fkCommand.ExecuteScalarAsync();
        referencedTable.Should().BeNull("不可以對 ProductReference 建立任何 Product 外鍵");
    }
}
