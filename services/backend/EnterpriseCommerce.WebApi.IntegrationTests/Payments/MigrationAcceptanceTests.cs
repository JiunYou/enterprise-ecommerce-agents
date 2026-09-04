using System;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

public class MigrationAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlContainer _mySqlContainer;

    public MigrationAcceptanceTests()
    {
        _mySqlContainer = new MySqlBuilder("mysql:8.0")
            .WithDatabase("migration_test_db")
            .WithUsername("test")
            .WithPassword("test")
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

    [Fact]
    public async Task EFCoreMigration_AddPaymentMVP_AppliesSuccessfully_AndCreatesExpectedSchema()
    {
        // Arrange
        var connectionString = _mySqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        await using var dbContext = new EnterpriseCommerceDbContext(options);

        // Act - Run actual migrations instead of EnsureCreated
        await dbContext.Database.MigrateAsync();

        // Assert - Verify migrations table has AddPaymentMVP
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("AddPaymentMVP"));

        // Assert - Verify PaymentAttempt unique constraints using raw SQL
        var constraintQuery = @"
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = 'migration_test_db'
              AND TABLE_NAME = 'PaymentAttempts'
              AND CONSTRAINT_TYPE = 'UNIQUE'";

        var attemptUniqueConstraints = await dbContext.Database.ExecuteSqlRawAsync(constraintQuery);
        // We know we should have unique constraints (OrderId, IdempotencyKey) and (Provider, ProviderTransactionId)
        // EF might create them as separate keys. ExecuteSqlRawAsync returns the number of rows affected (not useful for SELECT).
        // Let's use a proper query.

        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT INDEX_NAME 
            FROM information_schema.STATISTICS 
            WHERE TABLE_SCHEMA = 'migration_test_db' 
              AND TABLE_NAME = 'PaymentAttempts' 
              AND NON_UNIQUE = 0";
        await dbContext.Database.OpenConnectionAsync();
        var uniqueIndexes = new System.Collections.Generic.List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                uniqueIndexes.Add(reader.GetString(0));
            }
        }

        uniqueIndexes.Should().Contain("IX_PaymentAttempts_OrderId_IdempotencyKey");
        uniqueIndexes.Should().Contain("IX_PaymentAttempts_Provider_ProviderTransactionId");

        // Assert - Verify PaymentWebhookReceipt unique constraint
        command.CommandText = @"
            SELECT INDEX_NAME 
            FROM information_schema.STATISTICS 
            WHERE TABLE_SCHEMA = 'migration_test_db' 
              AND TABLE_NAME = 'PaymentWebhookReceipts' 
              AND NON_UNIQUE = 0";
              
        var receiptUniqueIndexes = new System.Collections.Generic.List<string>();
        await using (var receiptReader = await command.ExecuteReaderAsync())
        {
            while (await receiptReader.ReadAsync())
            {
                receiptUniqueIndexes.Add(receiptReader.GetString(0));
            }
        }

        receiptUniqueIndexes.Should().Contain("IX_PaymentWebhookReceipts_Provider_ProviderEventId");
    }

    [Fact]
    public async Task EFCoreMigration_AddOrderShippingAddress_AppliesSuccessfully_AndCreatesExpectedColumns()
    {
        // Arrange
        var connectionString = _mySqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        await using var dbContext = new EnterpriseCommerceDbContext(options);

        // Act - Run actual migrations
        await dbContext.Database.MigrateAsync();

        // Assert 1 - Verify migration applied
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("AddOrderShippingAddress"));

        // Assert 2 - Verify Orders table has 7 nullable shipping columns in MySQL
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = 'migration_test_db'
              AND TABLE_NAME = 'Orders'
              AND COLUMN_NAME LIKE 'Shipping%'";

        await dbContext.Database.OpenConnectionAsync();
        var shippingColumns = new System.Collections.Generic.Dictionary<string, (string IsNullable, string DataType, long? MaxLength)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var colName = reader.GetString(0);
                var isNullable = reader.GetString(1);
                var dataType = reader.GetString(2);
                long? maxLen = reader.IsDBNull(3) ? null : reader.GetInt64(3);
                shippingColumns[colName] = (isNullable, dataType, maxLen);
            }
        }

        shippingColumns.Should().ContainKey("ShippingRecipientName");
        shippingColumns["ShippingRecipientName"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingRecipientName"].MaxLength.Should().Be(100);

        shippingColumns.Should().ContainKey("ShippingPhone");
        shippingColumns["ShippingPhone"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingPhone"].MaxLength.Should().Be(30);

        shippingColumns.Should().ContainKey("ShippingCountryCode");
        shippingColumns["ShippingCountryCode"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingCountryCode"].MaxLength.Should().Be(2);

        shippingColumns.Should().ContainKey("ShippingPostalCode");
        shippingColumns["ShippingPostalCode"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingPostalCode"].MaxLength.Should().Be(20);

        shippingColumns.Should().ContainKey("ShippingCity");
        shippingColumns["ShippingCity"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingCity"].MaxLength.Should().Be(100);

        shippingColumns.Should().ContainKey("ShippingAddressLine1");
        shippingColumns["ShippingAddressLine1"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingAddressLine1"].MaxLength.Should().Be(200);

        shippingColumns.Should().ContainKey("ShippingAddressLine2");
        shippingColumns["ShippingAddressLine2"].IsNullable.Should().Be("YES");
        shippingColumns["ShippingAddressLine2"].MaxLength.Should().Be(200);

        // Assert 3 - Historical Null & New Snapshot Compatibility via Real MySQL
        var historicalOrder = EnterpriseCommerce.Domain.Orders.Order.Create(Guid.NewGuid(), "USD");
        historicalOrder.AddItem(new EnterpriseCommerce.Domain.Orders.ValueObjects.ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(50, "USD"), 1);
        historicalOrder.ChangeStatus(EnterpriseCommerce.Domain.Orders.OrderStatus.Submitted);

        var newOrder = EnterpriseCommerce.Domain.Orders.Order.Create(Guid.NewGuid(), "USD");
        newOrder.AddItem(new EnterpriseCommerce.Domain.Orders.ValueObjects.ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100, "USD"), 2);
        var syntheticShipping = EnterpriseCommerce.Domain.Orders.ValueObjects.ShippingAddress.Create(
            "Test Recipient", "0912345678", "TW", "100", "Taipei", "123 Main St", "Apt 1B").Value;
        newOrder.Submit(syntheticShipping, DateTimeOffset.UtcNow);

        dbContext.Orders.AddRange(historicalOrder, newOrder);
        await dbContext.SaveChangesAsync();

        // Reload via fresh context
        await using var verifyContext = new EnterpriseCommerceDbContext(options);
        var reloadedHistorical = await verifyContext.Orders.SingleAsync(o => o.Id == historicalOrder.Id);
        reloadedHistorical.ShippingAddress.Should().BeNull();

        var reloadedNew = await verifyContext.Orders.SingleAsync(o => o.Id == newOrder.Id);
        reloadedNew.ShippingAddress.Should().NotBeNull();
        reloadedNew.ShippingAddress!.RecipientName.Should().Be("Test Recipient");
        reloadedNew.ShippingAddress.AddressLine1.Should().Be("123 Main St");
        reloadedNew.ShippingAddress.CountryCode.Should().Be("TW");
    }
}
