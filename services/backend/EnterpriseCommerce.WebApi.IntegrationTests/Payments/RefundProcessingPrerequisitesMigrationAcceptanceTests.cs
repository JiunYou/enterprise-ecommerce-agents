using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

public class RefundProcessingPrerequisitesMigrationAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "refund_prerequisites_migration_test_db";
    private readonly MySqlContainer _mySqlContainer;
    private const string PreviousMigration = "20260905051022_AddAdminOrderCancellation";

    public RefundProcessingPrerequisitesMigrationAcceptanceTests()
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
            DROP TABLE IF EXISTS PaymentRefunds;
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

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";

        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@dbName";
        p1.Value = DatabaseName;
        cmd.Parameters.Add(p1);

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@tableName";
        p2.Value = tableName;
        cmd.Parameters.Add(p2);

        var p3 = cmd.CreateParameter();
        p3.ParameterName = "@columnName";
        p3.Value = columnName;
        cmd.Parameters.Add(p3);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    [Fact]
    public async Task MIGRATION_UPGRADE_PREVIOUS_TO_REFUND_PREREQUISITES_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // 1. Apply migrations through prior latest only
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        // 2. Verify PaymentRefunds and ProviderAuthorizationReference do NOT exist yet
        var dbConn = dbContext.Database.GetDbConnection();
        if (dbConn.State != System.Data.ConnectionState.Open)
        {
            await dbConn.OpenAsync();
        }

        var tableExistsBefore = await TableExistsAsync(dbConn, "PaymentRefunds");
        tableExistsBefore.Should().BeFalse("PaymentRefunds must not exist prior to the new migration.");

        var colExistsBefore = await ColumnExistsAsync(dbConn, "PaymentAttempts", "ProviderAuthorizationReference");
        colExistsBefore.Should().BeFalse("ProviderAuthorizationReference column must not exist prior to new migration.");

        // 3. Insert a legacy PaymentAttempt prior to upgrade to prove legacy retention and NULL column backfill
        var legacyAttemptId = Guid.NewGuid();
        var legacyOrderId = Guid.NewGuid();
        var legacyIdempotencyKey = Guid.NewGuid();
        var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

        await using (var insertCmd = dbConn.CreateCommand())
        {
            insertCmd.CommandText = $@"
                INSERT INTO PaymentAttempts (Id, OrderId, Amount, Currency, Provider, Status, IdempotencyKey, CreatedAt, Version)
                VALUES ('{legacyAttemptId}', '{legacyOrderId}', 100.00, 'TWD', 'ECPay', 'Pending', '{legacyIdempotencyKey}', '{nowStr}', 0);";
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 4. Apply the new migration
        await dbContext.Database.MigrateAsync();

        // 5. Verify table and column now exist
        var tableExistsAfter = await TableExistsAsync(dbConn, "PaymentRefunds");
        tableExistsAfter.Should().BeTrue("PaymentRefunds table must exist after migration is applied.");

        var colExistsAfter = await ColumnExistsAsync(dbConn, "PaymentAttempts", "ProviderAuthorizationReference");
        colExistsAfter.Should().BeTrue("ProviderAuthorizationReference column must exist after migration is applied.");

        // 6. Verify legacy PaymentAttempt is retained and ProviderAuthorizationReference is NULL
        await using (var verifyCmd = dbConn.CreateCommand())
        {
            verifyCmd.CommandText = $"SELECT ProviderAuthorizationReference, Status FROM PaymentAttempts WHERE Id = '{legacyAttemptId}';";
            await using var reader = await verifyCmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue("Legacy PaymentAttempt must be retained after migration upgrade.");
            reader.IsDBNull(0).Should().BeTrue("Legacy PaymentAttempt must have NULL ProviderAuthorizationReference.");
            reader.GetString(1).Should().Be("Pending");
        }

        // 7. Verify migration history contains the new migration
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(m => m.Contains("AddRefundProcessingPrerequisites"));
    }

    [Fact]
    public async Task MIGRATION_FRESH_DATABASE_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // Apply ALL current migrations from zero
        await dbContext.Database.MigrateAsync();

        var dbConn = dbContext.Database.GetDbConnection();
        var tableExists = await TableExistsAsync(dbConn, "PaymentRefunds");
        tableExists.Should().BeTrue("PaymentRefunds table must exist on fresh database.");

        var colExists = await ColumnExistsAsync(dbConn, "PaymentAttempts", "ProviderAuthorizationReference");
        colExists.Should().BeTrue("ProviderAuthorizationReference column must exist on fresh database.");

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(m => m.Contains("AddRefundProcessingPrerequisites"));
    }

    [Fact]
    public async Task MIGRATION_DOWN_TO_PREVIOUS_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);

        // Migrate to latest
        await dbContext.Database.MigrateAsync();
        var dbConn = dbContext.Database.GetDbConnection();
        var tableExistsLatest = await TableExistsAsync(dbConn, "PaymentRefunds");
        tableExistsLatest.Should().BeTrue();

        var colExistsLatest = await ColumnExistsAsync(dbConn, "PaymentAttempts", "ProviderAuthorizationReference");
        colExistsLatest.Should().BeTrue();

        // Migrate back to previous migration
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        // Verify table and column removed
        var tableExistsAfterDown = await TableExistsAsync(dbConn, "PaymentRefunds");
        tableExistsAfterDown.Should().BeFalse("PaymentRefunds table must be dropped on down migration.");

        var colExistsAfterDown = await ColumnExistsAsync(dbConn, "PaymentAttempts", "ProviderAuthorizationReference");
        colExistsAfterDown.Should().BeFalse("ProviderAuthorizationReference column must be dropped on down migration.");

        // Verify previous schema remains operational
        var paymentAttemptsExist = await TableExistsAsync(dbConn, "PaymentAttempts");
        paymentAttemptsExist.Should().BeTrue("PaymentAttempts table must remain intact after rolling back.");

        var adminOrderCancellationsExist = await TableExistsAsync(dbConn, "AdminOrderCancellations");
        adminOrderCancellationsExist.Should().BeTrue("AdminOrderCancellations table must remain intact after rolling back.");

        var ordersExist = await TableExistsAsync(dbConn, "Orders");
        ordersExist.Should().BeTrue("Orders table must remain intact after rolling back.");
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

        // 1. Column Assertions for PaymentAttempts.ProviderAuthorizationReference
        await using var cmdAttempt = conn.CreateCommand();
        cmdAttempt.CommandText = @"
            SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'PaymentAttempts' AND COLUMN_NAME = 'ProviderAuthorizationReference';";

        var pDb = cmdAttempt.CreateParameter();
        pDb.ParameterName = "@dbName";
        pDb.Value = DatabaseName;
        cmdAttempt.Parameters.Add(pDb);

        await using (var reader = await cmdAttempt.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue("ProviderAuthorizationReference column must exist");
            reader.GetString(0).ToLowerInvariant().Should().Be("varchar");
            reader.GetInt64(1).Should().Be(100);
            reader.GetString(2).Should().Be("YES");
        }

        // 2. Column Assertions for PaymentRefunds
        await using var cmdRefund = conn.CreateCommand();
        cmdRefund.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, COLLATION_NAME, IS_NULLABLE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'PaymentRefunds'
            ORDER BY ORDINAL_POSITION;";

        var pDbRefund = cmdRefund.CreateParameter();
        pDbRefund.ParameterName = "@dbName";
        pDbRefund.Value = DatabaseName;
        cmdRefund.Parameters.Add(pDbRefund);

        var cols = new Dictionary<string, (string DataType, long? MaxLen, string? Collation, string IsNullable)>();
        await using (var reader = await cmdRefund.ExecuteReaderAsync())
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

        cols.Should().HaveCount(8);

        // PaymentAttemptId
        cols.Should().ContainKey("PaymentAttemptId");
        cols["PaymentAttemptId"].DataType.ToLowerInvariant().Should().Be("char");
        cols["PaymentAttemptId"].MaxLen.Should().Be(36);
        cols["PaymentAttemptId"].IsNullable.Should().Be("NO");

        // Status
        cols.Should().ContainKey("Status");
        cols["Status"].DataType.ToLowerInvariant().Should().Contain("text");
        cols["Status"].IsNullable.Should().Be("NO");

        // Reason
        cols.Should().ContainKey("Reason");
        cols["Reason"].DataType.ToLowerInvariant().Should().Be("varchar");
        cols["Reason"].MaxLen.Should().Be(500);
        cols["Reason"].Collation.Should().Contain("utf8mb4");
        cols["Reason"].IsNullable.Should().Be("NO");

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

        // RequestedAt
        cols.Should().ContainKey("RequestedAt");
        cols["RequestedAt"].DataType.ToLowerInvariant().Should().Be("datetime");
        cols["RequestedAt"].IsNullable.Should().Be("NO");

        // CompletedAt
        cols.Should().ContainKey("CompletedAt");
        cols["CompletedAt"].DataType.ToLowerInvariant().Should().Be("datetime");
        cols["CompletedAt"].IsNullable.Should().Be("YES");

        // Version
        cols.Should().ContainKey("Version");
        cols["Version"].DataType.ToLowerInvariant().Should().Be("int");
        cols["Version"].IsNullable.Should().Be("NO");

        // 3. PK Assertion
        await using var pkCmd = conn.CreateCommand();
        pkCmd.CommandText = @"
            SELECT COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'PaymentRefunds' 
              AND CONSTRAINT_NAME = 'PRIMARY';";
        var pkParam = pkCmd.CreateParameter();
        pkParam.ParameterName = "@dbName";
        pkParam.Value = DatabaseName;
        pkCmd.Parameters.Add(pkParam);

        var pkCol = Convert.ToString(await pkCmd.ExecuteScalarAsync());
        pkCol.Should().Be("PaymentAttemptId");

        // 4. FK Assertion
        await using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = @"
            SELECT kcu.REFERENCED_TABLE_NAME, kcu.REFERENCED_COLUMN_NAME, rc.DELETE_RULE
            FROM information_schema.KEY_COLUMN_USAGE kcu
            JOIN information_schema.REFERENTIAL_CONSTRAINTS rc 
              ON kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME 
             AND kcu.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA
            WHERE kcu.TABLE_SCHEMA = @dbName 
              AND kcu.TABLE_NAME = 'PaymentRefunds' 
              AND kcu.REFERENCED_TABLE_NAME IS NOT NULL;";
        var fkParam = fkCmd.CreateParameter();
        fkParam.ParameterName = "@dbName";
        fkParam.Value = DatabaseName;
        fkCmd.Parameters.Add(fkParam);

        string? refTable = null;
        string? refCol = null;
        string? deleteRule = null;
        await using (var fkReader = await fkCmd.ExecuteReaderAsync())
        {
            if (await fkReader.ReadAsync())
            {
                refTable = fkReader.GetString(0);
                refCol = fkReader.GetString(1);
                deleteRule = fkReader.GetString(2);
            }
        }
        refTable.Should().Be("PaymentAttempts");
        refCol.Should().Be("Id");
        deleteRule.Should().Be("RESTRICT");

        // 5. Index Assertions: No unapproved extra index on PaymentRefunds
        await using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = @"
            SELECT DISTINCT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'PaymentRefunds' 
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
        extraIndexes.Should().BeEmpty("No speculative additional index should be created on PaymentRefunds.");

        // 6. Verify no index created specifically on ProviderAuthorizationReference in PaymentAttempts
        await using var attemptIdxCmd = conn.CreateCommand();
        attemptIdxCmd.CommandText = @"
            SELECT DISTINCT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @dbName 
              AND TABLE_NAME = 'PaymentAttempts' 
              AND COLUMN_NAME = 'ProviderAuthorizationReference';";
        var attemptIdxParam = attemptIdxCmd.CreateParameter();
        attemptIdxParam.ParameterName = "@dbName";
        attemptIdxParam.Value = DatabaseName;
        attemptIdxCmd.Parameters.Add(attemptIdxParam);

        var attemptIndexes = new List<string>();
        await using (var reader = await attemptIdxCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                attemptIndexes.Add(reader.GetString(0));
            }
        }
        attemptIndexes.Should().BeEmpty("ProviderAuthorizationReference must not have an index.");
    }

    [Fact]
    public async Task PERSISTENCE_BEHAVIOR_ACCEPTANCE_Passes()
    {
        await using var dbContext = CreateDbContext();
        await ResetDatabaseAsync(dbContext);
        await dbContext.Database.MigrateAsync();

        // 1. Create a valid Order & PaymentAttempt in MySQL
        var order = Order.Create(Guid.NewGuid(), "TWD");
        dbContext.Orders.Add(order);

        const string rawRefWithSpaces = "  GWSR_AUTH_REF_12345  ";
        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(500m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            rawRefWithSpaces);

        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        // Verify PaymentAttempt saved ProviderAuthorizationReference with EXACT value preserved
        await using (var verifyAttemptContext = CreateDbContext())
        {
            var reloadedAttempt = await verifyAttemptContext.PaymentAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attempt.Id);

            reloadedAttempt.Should().NotBeNull();
            reloadedAttempt!.ProviderAuthorizationReference.Should().Be(rawRefWithSpaces);
        }

        // 2. Round-trip valid PaymentRefund with Unicode Reason
        var requestedAt = DateTimeOffset.UtcNow;
        const string issuer = "https://auth.example.com/";
        const string subject = "auth0|admin-agent-999";
        const string unicodeReason = "訂單取消觸發全額退款處理 — ECPay 授權退款（繁體中文測試）";

        var refundResult = PaymentRefund.Create(
            attempt.Id,
            unicodeReason,
            issuer,
            subject,
            requestedAt);

        refundResult.IsSuccess.Should().BeTrue();
        var refund = refundResult.Value;

        dbContext.PaymentRefunds.Add(refund);
        await dbContext.SaveChangesAsync();

        // Verify in fresh DbContext with AsNoTracking
        await using (var verifyRefundContext = CreateDbContext())
        {
            var reloadedRefund = await verifyRefundContext.PaymentRefunds
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attempt.Id);

            reloadedRefund.Should().NotBeNull();
            reloadedRefund!.Id.Value.Should().Be(attempt.Id.Value);
            reloadedRefund.Status.Should().Be(PaymentRefundStatus.Pending);
            reloadedRefund.Reason.Should().Be(unicodeReason);
            reloadedRefund.ActorIssuer.Should().Be(issuer);
            reloadedRefund.ActorSubject.Should().Be(subject);
            reloadedRefund.RequestedAt.Should().BeCloseTo(requestedAt, TimeSpan.FromMilliseconds(50));
            reloadedRefund.CompletedAt.Should().BeNull();
            reloadedRefund.Version.Should().Be(0u);
        }

        // 3. Case-sensitive actor semantics (ascii_bin collation verification)
        var dbConn = dbContext.Database.GetDbConnection();
        if (dbConn.State != System.Data.ConnectionState.Open)
        {
            await dbConn.OpenAsync();
        }
        await using (var caseCmd = dbConn.CreateCommand())
        {
            // Exact case matches
            caseCmd.CommandText = "SELECT COUNT(*) FROM PaymentRefunds WHERE ActorSubject = 'auth0|admin-agent-999';";
            var exactMatch = Convert.ToInt32(await caseCmd.ExecuteScalarAsync());
            exactMatch.Should().Be(1);

            // Uppercase does not match because of ascii_bin collation
            caseCmd.CommandText = "SELECT COUNT(*) FROM PaymentRefunds WHERE ActorSubject = 'AUTH0|ADMIN-AGENT-999';";
            var upperMatch = Convert.ToInt32(await caseCmd.ExecuteScalarAsync());
            upperMatch.Should().Be(0, "ascii_bin collation must enforce case sensitivity");
        }

        // 4. State transition round-trip & Version increment: Pending -> Succeeded
        var completedAt = DateTimeOffset.UtcNow;
        refund.MarkAsSucceeded(completedAt).IsSuccess.Should().BeTrue();
        await dbContext.SaveChangesAsync();

        await using (var verifyTransitionContext = CreateDbContext())
        {
            var reloadedTransition = await verifyTransitionContext.PaymentRefunds
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attempt.Id);

            reloadedTransition.Should().NotBeNull();
            reloadedTransition!.Status.Should().Be(PaymentRefundStatus.Succeeded);
            reloadedTransition.CompletedAt.Should().NotBeNull();
            reloadedTransition.CompletedAt.Value.Should().BeCloseTo(completedAt, TimeSpan.FromMilliseconds(50));
            reloadedTransition.Version.Should().Be(1u, "Version must increment on legitimate update");
        }

        // 5. Version Concurrency Machine Evidence: Stale concurrency update is rejected
        // Create an active pending refund to test stale concurrency
        var orderConc = Order.Create(Guid.NewGuid(), "TWD");
        var attemptConc = PaymentAttempt.Create(orderConc.Id, new Money(200m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        dbContext.Orders.Add(orderConc);
        dbContext.PaymentAttempts.Add(attemptConc);
        var refundConc = PaymentRefund.Create(attemptConc.Id, "Reason for concurrency test", issuer, subject, DateTimeOffset.UtcNow).Value;
        dbContext.PaymentRefunds.Add(refundConc);
        await dbContext.SaveChangesAsync();

        // Load entity in Context A
        await using var contextA = CreateDbContext();
        var entityA = await contextA.PaymentRefunds.SingleAsync(x => x.Id == attemptConc.Id);

        // Load entity in Context B and perform legitimate update
        await using var contextB = CreateDbContext();
        var entityB = await contextB.PaymentRefunds.SingleAsync(x => x.Id == attemptConc.Id);
        entityB.MarkAsUnresolved().IsSuccess.Should().BeTrue();
        await contextB.SaveChangesAsync(); // entityB Version becomes 1

        // Attempt update in Context A with stale version (Version 0)
        entityA.MarkAsFailed(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        var actStale = async () => await contextA.SaveChangesAsync();
        await actStale.Should().ThrowAsync<DbUpdateConcurrencyException>("Stale concurrency update must be rejected");

        // 6. Duplicate PK: Second refund row for the SAME PaymentAttemptId is rejected by PK in real MySQL
        await using var duplicateContext = CreateDbContext();
        var duplicateRefund = PaymentRefund.Create(
            attempt.Id,
            "Duplicate refund attempt",
            issuer,
            subject,
            DateTimeOffset.UtcNow).Value;

        duplicateContext.PaymentRefunds.Add(duplicateRefund);
        var actDuplicate = async () => await duplicateContext.SaveChangesAsync();
        await actDuplicate.Should().ThrowAsync<DbUpdateException>("Duplicate PaymentAttemptId must violate PK constraint in MySQL.");

        // 7. FK constraint: Refund for nonexistent PaymentAttemptId is rejected in real MySQL
        await using var fkTestContext = CreateDbContext();
        var orphanRefund = PaymentRefund.Create(
            new PaymentAttemptId(Guid.NewGuid()),
            "Orphan refund",
            issuer,
            subject,
            DateTimeOffset.UtcNow).Value;

        fkTestContext.PaymentRefunds.Add(orphanRefund);
        var actOrphan = async () => await fkTestContext.SaveChangesAsync();
        await actOrphan.Should().ThrowAsync<DbUpdateException>("Refund referencing nonexistent PaymentAttempt must violate FK constraint in MySQL.");

        // 8. Delete Restriction (RESTRICT): Attempting to delete PaymentAttempt while PaymentRefund references it is rejected
        await using var deleteRestrictContext = CreateDbContext();
        var attemptToDelete = await deleteRestrictContext.PaymentAttempts.SingleAsync(x => x.Id == attempt.Id);
        deleteRestrictContext.PaymentAttempts.Remove(attemptToDelete);
        var actDelete = async () => await deleteRestrictContext.SaveChangesAsync();
        await actDelete.Should().ThrowAsync<DbUpdateException>("Deleting PaymentAttempt referenced by PaymentRefund must violate RESTRICT FK in MySQL.");

        // 9. Length boundary assertions in MySQL
        // ActorIssuer boundary: 512 accepted
        var orderBoundary = Order.Create(Guid.NewGuid(), "TWD");
        var attemptBoundary = PaymentAttempt.Create(orderBoundary.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        dbContext.Orders.Add(orderBoundary);
        dbContext.PaymentAttempts.Add(attemptBoundary);
        await dbContext.SaveChangesAsync();

        var refund512 = PaymentRefund.Create(attemptBoundary.Id, "Valid reason", new string('i', 512), "sub", DateTimeOffset.UtcNow).Value;
        dbContext.PaymentRefunds.Add(refund512);
        await dbContext.SaveChangesAsync();

        // ActorIssuer boundary: 513 rejected in domain & database
        var domain513 = PaymentRefund.Create(attemptBoundary.Id, "Valid reason", new string('i', 513), "sub", DateTimeOffset.UtcNow);
        domain513.IsFailure.Should().BeTrue();
        domain513.Error.Should().Be(PaymentErrors.InvalidActorIssuer);

        // ActorSubject boundary: 255 accepted
        var orderSub = Order.Create(Guid.NewGuid(), "TWD");
        var attemptSub = PaymentAttempt.Create(orderSub.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        dbContext.Orders.Add(orderSub);
        dbContext.PaymentAttempts.Add(attemptSub);
        await dbContext.SaveChangesAsync();

        var refundSub255 = PaymentRefund.Create(attemptSub.Id, "Valid reason", "issuer", new string('s', 255), DateTimeOffset.UtcNow).Value;
        dbContext.PaymentRefunds.Add(refundSub255);
        await dbContext.SaveChangesAsync();

        // ActorSubject boundary: 256 rejected in domain
        var domainSub256 = PaymentRefund.Create(attemptSub.Id, "Valid reason", "issuer", new string('s', 256), DateTimeOffset.UtcNow);
        domainSub256.IsFailure.Should().BeTrue();
        domainSub256.Error.Should().Be(PaymentErrors.InvalidActorSubject);

        // Reason boundary: 500 accepted
        var order500 = Order.Create(Guid.NewGuid(), "TWD");
        var attempt500 = PaymentAttempt.Create(order500.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        dbContext.Orders.Add(order500);
        dbContext.PaymentAttempts.Add(attempt500);
        await dbContext.SaveChangesAsync();

        var refund500 = PaymentRefund.Create(attempt500.Id, new string('測', 500), issuer, subject, DateTimeOffset.UtcNow).Value;
        dbContext.PaymentRefunds.Add(refund500);
        await dbContext.SaveChangesAsync();

        // Reason boundary: 501 rejected in domain
        var domainReason501 = PaymentRefund.Create(attempt500.Id, new string('測', 501), issuer, subject, DateTimeOffset.UtcNow);
        domainReason501.IsFailure.Should().BeTrue();
        domainReason501.Error.Should().Be(PaymentErrors.InvalidRefundReason);

        await using var verifyBoundariesContext = CreateDbContext();
        var reloaded500 = await verifyBoundariesContext.PaymentRefunds.SingleAsync(x => x.Id == attempt500.Id);
        reloaded500.Reason.Length.Should().Be(500);
    }
}
