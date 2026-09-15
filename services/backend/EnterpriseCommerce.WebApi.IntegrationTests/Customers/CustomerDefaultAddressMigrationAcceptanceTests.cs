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

public class CustomerDefaultAddressMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "customer_default_address_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public CustomerDefaultAddressMigrationAcceptanceTests()
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

    private static (string PreviousMigration, string TargetMigration, int TotalMigrations) ResolveMigrations(EnterpriseCommerceDbContext dbContext)
    {
        var allMigrations = dbContext.Database.GetMigrations().ToList();
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddCustomerDefaultAddress"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddCustomerDefaultAddress does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddCustomerDefaultAddress is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration, allMigrations.Count);
    }

    [Fact]
    public async Task Migration_AddCustomerDefaultAddress_Lifecycle_Upgrade_Fresh_Down_Acceptance()
    {
        await using var dbContext = CreateDbContext();
        var (previousMigration, targetMigration, totalMigrations) = ResolveMigrations(dbContext);

        totalMigrations.Should().Be(19, "遷移總數必須剛好為前版 (18) + 1 = 19");
        previousMigration.Should().Contain("AddFixedAmountCoupons");
        targetMigration.Should().Contain("AddCustomerDefaultAddress");

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();

        // 1. 遷移至前一個最新版本 AddFixedAmountCoupons
        await migrator.MigrateAsync(previousMigration);

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // 插入一筆既有地址記錄以驗證遷移相容性
        var existingAddressId = Guid.NewGuid().ToString();
        var existingCustomerId = Guid.NewGuid().ToString();
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO CustomerAddresses 
                (Id, CustomerId, RecipientName, Phone, CountryCode, PostalCode, City, AddressLine1, AddressLine2, CreatedAt)
                VALUES 
                (@id, @customerId, '原有客戶', '0912345678', 'TW', '100', '台北市', '忠孝西路一段', NULL, NOW(6))";
            var pId = insertCmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = existingAddressId;
            insertCmd.Parameters.Add(pId);

            var pCust = insertCmd.CreateParameter();
            pCust.ParameterName = "@customerId";
            pCust.Value = existingCustomerId;
            insertCmd.Parameters.Add(pCust);

            await insertCmd.ExecuteNonQueryAsync();
        }

        // 2. 應用目標遷移 AddCustomerDefaultAddress
        await migrator.MigrateAsync(targetMigration);

        var columns = await GetTableColumnsAsync(conn, "CustomerAddresses");
        columns.Should().ContainKey("IsDefault", "CustomerAddresses 資料表必須包含 IsDefault 欄位");
        columns["IsDefault"].ToLowerInvariant().Should().Contain("tinyint");

        // 驗證既有資料列遷移後 IsDefault 為 false (0)
        await using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = "SELECT IsDefault FROM CustomerAddresses WHERE Id = @id";
            var p = queryCmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = existingAddressId;
            queryCmd.Parameters.Add(p);

            var isDefaultValue = Convert.ToBoolean(await queryCmd.ExecuteScalarAsync());
            isDefaultValue.Should().BeFalse("既有資料列在遷移後 IsDefault 必須預設為 false");
        }

        // 3. 測試 Down：回復至 previousMigration (AddFixedAmountCoupons)，確認 IsDefault 欄位已被移除
        await migrator.MigrateAsync(previousMigration);
        var columnsAfterDown = await GetTableColumnsAsync(conn, "CustomerAddresses");
        columnsAfterDown.Should().NotContainKey("IsDefault", "Down 之後 CustomerAddresses 不應包含 IsDefault 欄位");

        // 4. 測試 Fresh 完整升級至最新
        await migrator.MigrateAsync();
        var columnsFresh = await GetTableColumnsAsync(conn, "CustomerAddresses");
        columnsFresh.Should().ContainKey("IsDefault", "Fresh 完整升級之後 CustomerAddresses 必須存在 IsDefault 欄位");
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
}
