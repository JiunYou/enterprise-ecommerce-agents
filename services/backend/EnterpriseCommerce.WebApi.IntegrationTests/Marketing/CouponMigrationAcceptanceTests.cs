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

public class CouponMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "coupon_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public CouponMigrationAcceptanceTests()
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
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddFixedAmountCoupons"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddFixedAmountCoupons does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddFixedAmountCoupons is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    [Fact]
    public async Task Migration_AddFixedAmountCoupons_Lifecycle_Upgrade_Fresh_Down_Acceptance()
    {
        await using var dbContext = CreateDbContext();
        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        previousMigration.Should().Contain("AddCustomerAddresses");
        targetMigration.Should().Contain("AddFixedAmountCoupons");

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // 1. 遷移至前一個最新版本 AddCustomerAddresses
        await migrator.MigrateAsync(previousMigration);

        // 2. 應用目標遷移 AddFixedAmountCoupons
        await migrator.MigrateAsync(targetMigration);

        // 驗證 Coupons 資料表存在及其欄位
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        var couponColumns = await GetTableColumnsAsync(conn, "Coupons");
        couponColumns.Should().ContainKey("Id");
        couponColumns.Should().ContainKey("Code");
        couponColumns.Should().ContainKey("DiscountAmount");
        couponColumns.Should().ContainKey("Currency");
        couponColumns.Should().ContainKey("StartsAt");
        couponColumns.Should().ContainKey("ExpiresAt");
        couponColumns.Should().ContainKey("IsActive");
        couponColumns.Should().ContainKey("CreatedAt");

        // 驗證 Orders 資料表具有 nullable 的優惠券 snapshot 欄位
        var orderColumns = await GetTableColumnsAsync(conn, "Orders");
        orderColumns.Should().ContainKey("AppliedCouponCode");
        orderColumns.Should().ContainKey("AppliedCouponDiscountAmount");
        orderColumns.Should().ContainKey("AppliedCouponExpiresAt");

        // 驗證 Coupons.Code 具有唯一索引
        var hasCodeUniqueIndex = await HasUniqueIndexOnColumnAsync(conn, "Coupons", "Code");
        hasCodeUniqueIndex.Should().BeTrue("Coupons 資料表必須對 Code 具備唯一索引約束");

        // 驗證 Coupons 資料表無外鍵 (COUPON_ORDER_FOREIGN_KEY_ADDED=NO, COUPON_CUSTOMER_FOREIGN_KEY_ADDED=NO, COUPON_PRODUCT_FOREIGN_KEY_ADDED=NO)
        var couponFkCount = await GetForeignKeyCountAsync(conn, "Coupons");
        couponFkCount.Should().Be(0, "Coupons 不應包含任何外鍵約束");

        // 驗證 Orders 資料表沒有指向 Coupons 的外鍵 (ORDER_COUPON_FOREIGN_KEY_ADDED=NO)
        var orderToCouponFk = await HasForeignKeyBetweenTablesAsync(conn, "Orders", "Coupons");
        orderToCouponFk.Should().BeFalse("Orders 不應有任何指向 Coupons 的外鍵約束");

        // 3. 測試 Down：回復至 previousMigration (AddCustomerAddresses)，確認 Coupons 資料表被移除，Orders 中的欄位被移除
        await migrator.MigrateAsync(previousMigration);
        var tableExistsAfterDown = await TableExistsAsync(conn, "Coupons");
        tableExistsAfterDown.Should().BeFalse("Down 之後 Coupons 資料表必須被移除");

        var orderColumnsAfterDown = await GetTableColumnsAsync(conn, "Orders");
        orderColumnsAfterDown.Should().NotContainKey("AppliedCouponCode");
        orderColumnsAfterDown.Should().NotContainKey("AppliedCouponDiscountAmount");
        orderColumnsAfterDown.Should().NotContainKey("AppliedCouponExpiresAt");

        // 4. 測試 Fresh 完整升級至最新
        await migrator.MigrateAsync();
        var tableExistsFresh = await TableExistsAsync(conn, "Coupons");
        tableExistsFresh.Should().BeTrue("Fresh 升級之後 Coupons 資料表必須存在");
    }

    private static async Task<Dictionary<string, string>> GetTableColumnsAsync(DbConnection connection, string tableName)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
        var p = cmd.CreateParameter();
        p.ParameterName = "@TableName";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }
        return columns;
    }

    private static async Task<bool> HasUniqueIndexOnColumnAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.STATISTICS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = @TableName 
              AND COLUMN_NAME = @ColumnName 
              AND NON_UNIQUE = 0";
        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@TableName";
        p1.Value = tableName;
        cmd.Parameters.Add(p1);

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@ColumnName";
        p2.Value = columnName;
        cmd.Parameters.Add(p2);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<int> GetForeignKeyCountAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = @TableName 
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        var p = cmd.CreateParameter();
        p.ParameterName = "@TableName";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<bool> HasForeignKeyBetweenTablesAsync(DbConnection connection, string tableName, string referencedTableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = @TableName 
              AND REFERENCED_TABLE_NAME = @ReferencedTableName";
        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@TableName";
        p1.Value = tableName;
        cmd.Parameters.Add(p1);

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@ReferencedTableName";
        p2.Value = referencedTableName;
        cmd.Parameters.Add(p2);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
        var p = cmd.CreateParameter();
        p.ParameterName = "@TableName";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }
}
