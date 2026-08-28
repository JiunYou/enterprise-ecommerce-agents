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
}
