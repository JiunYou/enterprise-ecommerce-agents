using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Orders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

public class AdminOrderCancellationMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "admin_cancel_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;
    private const string PreviousMigration = "20260904155157_AddOrderShippingAddress";

    public AdminOrderCancellationMigrationAcceptanceTests()
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

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = @tableName;";

        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@dbName";
        p1.Value = DatabaseName;
        cmd.Parameters.Add(p1);

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@tableName";
        p2.Value = tableName;
        cmd.Parameters.Add(p2);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    [Fact]
    public async Task MIGRATION_UPGRADE_PREVIOUS_TO_ADMIN_CANCEL_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // 1. Apply migrations through prior latest only
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        // 2. Verify AdminOrderCancellations does NOT yet exist
        var dbConn = dbContext.Database.GetDbConnection();
        var existsBefore = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        existsBefore.Should().BeFalse("AdminOrderCancellations must not exist prior to the new migration.");

        // 3. Apply the new migration
        await dbContext.Database.MigrateAsync();

        // 4. Verify table now exists
        var existsAfter = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        existsAfter.Should().BeTrue("AdminOrderCancellations must exist after the new migration is applied.");

        // 5. Verify migration history contains the new migration
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(m => m.Contains("AddAdminOrderCancellation"));
    }

    [Fact]
    public async Task MIGRATION_FRESH_DATABASE_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // Apply ALL current migrations from zero
        await dbContext.Database.MigrateAsync();

        var dbConn = dbContext.Database.GetDbConnection();
        var exists = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        exists.Should().BeTrue();

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(m => m.Contains("AddAdminOrderCancellation"));
    }

    [Fact]
    public async Task MIGRATION_DOWN_ADMIN_CANCEL_TO_PREVIOUS_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // Migrate to latest
        await dbContext.Database.MigrateAsync();
        var dbConn = dbContext.Database.GetDbConnection();
        var existsLatest = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        existsLatest.Should().BeTrue();

        // Migrate back to previous migration
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        // Verify table removed
        var existsAfterDown = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        existsAfterDown.Should().BeFalse("Table must be dropped on down migration.");

        // Verify previous schema remains operational
        var ordersExist = await TableExistsAsync(dbConn, "Orders");
        ordersExist.Should().BeTrue("Orders table must remain intact after rolling back AdminOrderCancellations.");
    }

    [Fact]
    public async Task REAL_MYSQL_SCHEMA_ASSERTIONS_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);
        await dbContext.Database.MigrateAsync();

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // 1. Column Assertions
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, COLLATION_NAME, IS_NULLABLE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'AdminOrderCancellations'
            ORDER BY ORDINAL_POSITION;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@dbName";
        p.Value = DatabaseName;
        cmd.Parameters.Add(p);

        var cols = new Dictionary<string, (string DataType, long? MaxLen, string? Collation, string IsNullable)>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var dataType = reader.GetString(1);
                long? maxLen = reader.IsDBNull(2) ? null : reader.GetInt64(2);
                string? collation = reader.IsDBNull(3) ? null : reader.GetString(3);
                var isNullable = reader.GetString(4);
                cols[name] = (dataType, maxLen, collation, isNullable);
            }
        }

        cols.Should().HaveCount(5);

        // OrderId
        cols.Should().ContainKey("OrderId");
        cols["OrderId"].DataType.ToLowerInvariant().Should().Be("char");
        cols["OrderId"].MaxLen.Should().Be(36);
        cols["OrderId"].IsNullable.Should().Be("NO");

        // ActorIssuer
        cols.Should().ContainKey("ActorIssuer");
        cols["ActorIssuer"].DataType.ToLowerInvariant().Should().Be("varchar");
        cols["ActorIssuer"].MaxLen.Should().Be(512);
        cols["ActorIssuer"].Collation.Should().Be("ascii_bin");
        cols["ActorIssuer"].IsNullable.Should().Be("NO");

        // ActorSubject
        cols.Should().ContainKey("ActorSubject");
        cols["ActorSubject"].DataType.ToLowerInvariant().Should().Be("varchar");
        cols["ActorSubject"].MaxLen.Should().Be(255);
        cols["ActorSubject"].Collation.Should().Be("ascii_bin");
        cols["ActorSubject"].IsNullable.Should().Be("NO");

        // CancelledAt
        cols.Should().ContainKey("CancelledAt");
        cols["CancelledAt"].DataType.ToLowerInvariant().Should().Be("datetime");
        cols["CancelledAt"].IsNullable.Should().Be("NO");

        // Reason
        cols.Should().ContainKey("Reason");
        cols["Reason"].DataType.ToLowerInvariant().Should().Be("varchar");
        cols["Reason"].MaxLen.Should().Be(500);
        cols["Reason"].Collation.Should().Contain("utf8mb4");
        cols["Reason"].IsNullable.Should().Be("NO");

        // 2. PK Assertion
        await using var pkCmd = conn.CreateCommand();
        pkCmd.CommandText = @"
            SELECT COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'AdminOrderCancellations' 
              AND CONSTRAINT_NAME = 'PRIMARY';";
        var pkParam = pkCmd.CreateParameter();
        pkParam.ParameterName = "@dbName";
        pkParam.Value = DatabaseName;
        pkCmd.Parameters.Add(pkParam);

        var pkCol = Convert.ToString(await pkCmd.ExecuteScalarAsync());
        pkCol.Should().Be("OrderId");

        // 3. FK Assertion
        await using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = @"
            SELECT REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'AdminOrderCancellations' 
              AND REFERENCED_TABLE_NAME IS NOT NULL;";
        var fkParam = fkCmd.CreateParameter();
        fkParam.ParameterName = "@dbName";
        fkParam.Value = DatabaseName;
        fkCmd.Parameters.Add(fkParam);

        string? refTable = null;
        string? refCol = null;
        await using (var fkReader = await fkCmd.ExecuteReaderAsync())
        {
            if (await fkReader.ReadAsync())
            {
                refTable = fkReader.GetString(0);
                refCol = fkReader.GetString(1);
            }
        }
        refTable.Should().Be("Orders");
        refCol.Should().Be("Id");

        // 4. No Unapproved Extra Index
        await using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = @"
            SELECT DISTINCT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'AdminOrderCancellations' 
              AND INDEX_NAME != 'PRIMARY';";
        var idxParam = idxCmd.CreateParameter();
        idxParam.ParameterName = "@dbName";
        idxParam.Value = DatabaseName;
        idxCmd.Parameters.Add(idxParam);

        var extraIndexes = new List<string>();
        await using (var idxReader = await idxCmd.ExecuteReaderAsync())
        {
            while (await idxReader.ReadAsync())
            {
                extraIndexes.Add(idxReader.GetString(0));
            }
        }
        extraIndexes.Should().BeEmpty("No speculative additional index should be created on AdminOrderCancellations.");
    }

    [Fact]
    public async Task PERSISTENCE_BEHAVIOR_ACCEPTANCE_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);
        await dbContext.Database.MigrateAsync();

        // 1. Create a valid Order in MySQL
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(100m, "USD"), 1);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        // 2. Round-trip valid AdminOrderCancellation with Unicode Reason
        var cancelledAt = DateTimeOffset.UtcNow;
        const string issuer = "https://auth.example.com/";
        const string subject = "auth0|admin-user-12345";
        const string unicodeReason = "顧客致電要求取消訂單（特例核准）— 庫存損耗瑕疵處理";

        var audit = AdminOrderCancellation.Create(
            order.Id,
            issuer,
            subject,
            cancelledAt,
            unicodeReason);

        dbContext.AdminOrderCancellations.Add(audit);
        await dbContext.SaveChangesAsync();

        // Verify in fresh DbContext with AsNoTracking
        await using var verifyContext = CreateDbContext();
        var reloaded = await verifyContext.AdminOrderCancellations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == order.Id);

        reloaded.Should().NotBeNull();
        reloaded!.OrderId.Value.Should().Be(order.Id.Value);
        reloaded.ActorIssuer.Should().Be(issuer);
        reloaded.ActorSubject.Should().Be(subject);
        reloaded.CancelledAt.Should().BeCloseTo(cancelledAt, TimeSpan.FromMilliseconds(50));
        reloaded.Reason.Should().Be(unicodeReason);

        // 3. Second audit row for the SAME OrderId is rejected by PK uniqueness in real MySQL
        await using var duplicateContext = CreateDbContext();
        var duplicateAudit = AdminOrderCancellation.Create(
            order.Id,
            "https://another.issuer.com/",
            "auth0|another-admin",
            DateTimeOffset.UtcNow,
            "Duplicate attempt");

        duplicateContext.AdminOrderCancellations.Add(duplicateAudit);
        var actDuplicate = async () => await duplicateContext.SaveChangesAsync();
        await actDuplicate.Should().ThrowAsync<DbUpdateException>("Duplicate OrderId must violate primary key constraint in MySQL.");

        // 4. Audit row for nonexistent OrderId is rejected by FK in real MySQL
        await using var fkTestContext = CreateDbContext();
        var orphanAudit = AdminOrderCancellation.Create(
            new OrderId(Guid.NewGuid()),
            issuer,
            subject,
            DateTimeOffset.UtcNow,
            "Orphan audit test");

        fkTestContext.AdminOrderCancellations.Add(orphanAudit);
        var actOrphan = async () => await fkTestContext.SaveChangesAsync();
        await actOrphan.Should().ThrowAsync<DbUpdateException>("Audit referencing nonexistent Order must violate FK constraint in MySQL.");

        // 5. Length boundary tests
        // ActorIssuer boundary: 512 accepted
        var order512 = Order.Create(Guid.NewGuid(), "USD");
        order512.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext = CreateDbContext();
        boundaryContext.Orders.Add(order512);

        var issuer512 = new string('a', 512);
        var audit512 = AdminOrderCancellation.Create(order512.Id, issuer512, "sub", DateTimeOffset.UtcNow, "Reason");
        boundaryContext.AdminOrderCancellations.Add(audit512);
        await boundaryContext.SaveChangesAsync();

        // ActorIssuer > 512 rejected by database
        var order513 = Order.Create(Guid.NewGuid(), "USD");
        order513.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext513 = CreateDbContext();
        boundaryContext513.Orders.Add(order513);
        var issuer513 = new string('a', 513);
        var audit513 = AdminOrderCancellation.Create(order513.Id, issuer513, "sub", DateTimeOffset.UtcNow, "Reason");
        boundaryContext513.AdminOrderCancellations.Add(audit513);
        var act513 = async () => await boundaryContext513.SaveChangesAsync();
        await act513.Should().ThrowAsync<DbUpdateException>();

        // ActorSubject boundary: 255 accepted
        var order255 = Order.Create(Guid.NewGuid(), "USD");
        order255.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext255 = CreateDbContext();
        boundaryContext255.Orders.Add(order255);
        var subject255 = new string('b', 255);
        var audit255 = AdminOrderCancellation.Create(order255.Id, "issuer", subject255, DateTimeOffset.UtcNow, "Reason");
        boundaryContext255.AdminOrderCancellations.Add(audit255);
        await boundaryContext255.SaveChangesAsync();

        // ActorSubject > 255 rejected by database
        var order256 = Order.Create(Guid.NewGuid(), "USD");
        order256.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext256 = CreateDbContext();
        boundaryContext256.Orders.Add(order256);
        var subject256 = new string('b', 256);
        var audit256 = AdminOrderCancellation.Create(order256.Id, "issuer", subject256, DateTimeOffset.UtcNow, "Reason");
        boundaryContext256.AdminOrderCancellations.Add(audit256);
        var act256 = async () => await boundaryContext256.SaveChangesAsync();
        await act256.Should().ThrowAsync<DbUpdateException>();

        // Reason boundary: 500 accepted
        var order500 = Order.Create(Guid.NewGuid(), "USD");
        order500.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext500 = CreateDbContext();
        boundaryContext500.Orders.Add(order500);
        var reason500 = new string('中', 500); // 500 Unicode chars
        var audit500 = AdminOrderCancellation.Create(order500.Id, "issuer", "sub", DateTimeOffset.UtcNow, reason500);
        boundaryContext500.AdminOrderCancellations.Add(audit500);
        await boundaryContext500.SaveChangesAsync();

        // Reason > 500 rejected by database
        var order501 = Order.Create(Guid.NewGuid(), "USD");
        order501.AddItem(new ProductId(Guid.NewGuid()), new Money(10m, "USD"), 1);
        await using var boundaryContext501 = CreateDbContext();
        boundaryContext501.Orders.Add(order501);
        var reason501 = new string('中', 501);
        var audit501 = AdminOrderCancellation.Create(order501.Id, "issuer", "sub", DateTimeOffset.UtcNow, reason501);
        boundaryContext501.AdminOrderCancellations.Add(audit501);
        var act501 = async () => await boundaryContext501.SaveChangesAsync();
        await act501.Should().ThrowAsync<DbUpdateException>();
    }
}
