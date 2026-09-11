using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

public class ShipmentTrackingMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "shipment_tracking_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;

    public ShipmentTrackingMigrationAcceptanceTests()
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
        var targetMigration = allMigrations.FirstOrDefault(m => m.Contains("AddOrderShipmentTracking"));
        if (targetMigration is null)
        {
            throw new InvalidOperationException("Migration AddOrderShipmentTracking does not exist yet.");
        }
        var targetIndex = allMigrations.IndexOf(targetMigration);
        if (targetIndex <= 0)
        {
            throw new InvalidOperationException("AddOrderShipmentTracking is the first migration or index invalid.");
        }
        var previousMigration = allMigrations[targetIndex - 1];
        return (previousMigration, targetMigration);
    }

    private static async Task<Dictionary<string, (string DataType, long? MaxLen, string IsNullable)>> GetOrderColumnsAsync(
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
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'Orders';";

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
    public async Task MIGRATION_UPGRADE_PREVIOUS_TO_SHIPMENT_TRACKING_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        // 1. Apply through previous migration
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(previousMigration);

        var conn = dbContext.Database.GetDbConnection();
        var colsBefore = await GetOrderColumnsAsync(conn);
        colsBefore.Should().NotContainKey("ShippingCarrier");
        colsBefore.Should().NotContainKey("ShippingTrackingNumber");
        colsBefore.Should().NotContainKey("ShippedAt");

        // 2. Apply target migration
        await migrator.MigrateAsync(targetMigration);

        var colsAfter = await GetOrderColumnsAsync(conn);
        colsAfter.Should().ContainKey("ShippingCarrier");
        colsAfter["ShippingCarrier"].IsNullable.Should().Be("YES");
        colsAfter["ShippingCarrier"].MaxLen.Should().Be(100);

        colsAfter.Should().ContainKey("ShippingTrackingNumber");
        colsAfter["ShippingTrackingNumber"].IsNullable.Should().Be("YES");
        colsAfter["ShippingTrackingNumber"].MaxLen.Should().Be(100);

        colsAfter.Should().ContainKey("ShippedAt");
        colsAfter["ShippedAt"].IsNullable.Should().Be("YES");

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
        var cols = await GetOrderColumnsAsync(conn);

        cols.Should().ContainKey("ShippingCarrier");
        cols["ShippingCarrier"].IsNullable.Should().Be("YES");
        cols.Should().ContainKey("ShippingTrackingNumber");
        cols["ShippingTrackingNumber"].IsNullable.Should().Be("YES");
        cols.Should().ContainKey("ShippedAt");
        cols["ShippedAt"].IsNullable.Should().Be("YES");

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(targetMigration);
    }

    [Fact]
    public async Task MIGRATION_HISTORICAL_COMPATIBILITY_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(previousMigration);

        // Insert historical Shipped order using raw SQL under previous migration
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO Orders (Id, CustomerId, Status, Currency, Version, SubmittedAt)
                VALUES (@id, @customerId, 'Shipped', 'USD', 1, NOW());";

            var p1 = insertCmd.CreateParameter();
            p1.ParameterName = "@id";
            p1.Value = orderId.ToString();
            insertCmd.Parameters.Add(p1);

            var p2 = insertCmd.CreateParameter();
            p2.ParameterName = "@customerId";
            p2.Value = customerId.ToString();
            insertCmd.Parameters.Add(p2);

            await insertCmd.ExecuteNonQueryAsync();
        }

        // Apply new migration
        await migrator.MigrateAsync(targetMigration);

        // Read using fresh DbContext
        await using var readDbContext = CreateDbContext();
        var loadedOrder = await readDbContext.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == new OrderId(orderId));

        loadedOrder.Should().NotBeNull();
        loadedOrder!.Status.Should().Be(OrderStatus.Shipped);
        loadedOrder.ShippingCarrier.Should().BeNull();
        loadedOrder.ShippingTrackingNumber.Should().BeNull();
        loadedOrder.ShippedAt.Should().BeNull();
    }

    [Fact]
    public async Task MIGRATION_DOWN_SHIPMENT_TRACKING_TO_PREVIOUS_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        var (previousMigration, targetMigration) = ResolveMigrations(dbContext);

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);

        var conn = dbContext.Database.GetDbConnection();
        var colsBeforeDown = await GetOrderColumnsAsync(conn);
        colsBeforeDown.Should().ContainKey("ShippingCarrier");

        // Migrate down to previous
        await migrator.MigrateAsync(previousMigration);

        var colsAfterDown = await GetOrderColumnsAsync(conn);
        colsAfterDown.Should().NotContainKey("ShippingCarrier");
        colsAfterDown.Should().NotContainKey("ShippingTrackingNumber");
        colsAfterDown.Should().NotContainKey("ShippedAt");

        // Orders table must remain intact
        colsAfterDown.Should().ContainKey("Id");
        colsAfterDown.Should().ContainKey("CustomerId");
        colsAfterDown.Should().ContainKey("Status");
    }
}
