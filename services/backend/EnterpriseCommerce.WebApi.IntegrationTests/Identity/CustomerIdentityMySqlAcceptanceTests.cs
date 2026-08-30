using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Identity;

public class CustomerIdentityMySqlAcceptanceTests : IAsyncLifetime
{
    private const string DatabaseName = "customer_identity_test_db";
    private readonly MySqlContainer _mySqlContainer;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CustomerIdentityMySqlAcceptanceTests()
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

        var connectionString = _mySqlContainer.GetConnectionString() + ";Max Pool Size=200;";
        _dbContextOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        // Apply real EF Core migrations on fresh MySQL database
        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _mySqlContainer.DisposeAsync();
    }

    private EnterpriseCommerceDbContext CreateDbContext() => new(_dbContextOptions);

    [Fact]
    public async Task Migration_AddCustomerIdentities_AppliesSuccessfully_AndCreatesExactSchemaAndIndex()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        // Assert 1: Migration is applied
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("AddCustomerIdentities"));

        // Assert 2: Table & Column schema from information_schema.COLUMNS
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_SET_NAME, COLLATION_NAME, IS_NULLABLE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'CustomerIdentities'
            ORDER BY ORDINAL_POSITION;";

        var dbParam = cmd.CreateParameter();
        dbParam.ParameterName = "@dbName";
        dbParam.Value = DatabaseName;
        cmd.Parameters.Add(dbParam);

        var columns = new List<(string Name, string DataType, long? MaxLength, string? Charset, string? Collation, string IsNullable)>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5)
                ));
            }
        }

        columns.Should().HaveCount(4);

        // Column Id: char(36)
        var idCol = columns.First(c => c.Name == "Id");
        idCol.DataType.ToLowerInvariant().Should().Be("char");
        idCol.MaxLength.Should().Be(36);
        idCol.IsNullable.Should().Be("NO");

        // Column Issuer: varchar(512), ascii, ascii_bin, NOT NULL
        var issuerCol = columns.First(c => c.Name == "Issuer");
        issuerCol.DataType.ToLowerInvariant().Should().Be("varchar");
        issuerCol.MaxLength.Should().Be(512);
        issuerCol.Charset.Should().Be("ascii");
        issuerCol.Collation.Should().Be("ascii_bin");
        issuerCol.IsNullable.Should().Be("NO");

        // Column Subject: varchar(255), ascii, ascii_bin, NOT NULL
        var subjectCol = columns.First(c => c.Name == "Subject");
        subjectCol.DataType.ToLowerInvariant().Should().Be("varchar");
        subjectCol.MaxLength.Should().Be(255);
        subjectCol.Charset.Should().Be("ascii");
        subjectCol.Collation.Should().Be("ascii_bin");
        subjectCol.IsNullable.Should().Be("NO");

        // Column CreatedAt: datetime(6), NOT NULL
        var createdAtCol = columns.First(c => c.Name == "CreatedAt");
        createdAtCol.DataType.ToLowerInvariant().Should().Be("datetime");
        createdAtCol.IsNullable.Should().Be("NO");

        // Assert 3: Unique index from information_schema.STATISTICS
        await using var indexCmd = connection.CreateCommand();
        indexCmd.CommandText = @"
            SELECT INDEX_NAME, COLUMN_NAME, SEQ_IN_INDEX, NON_UNIQUE
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = 'CustomerIdentities' AND INDEX_NAME = 'IX_CustomerIdentities_Issuer_Subject'
            ORDER BY SEQ_IN_INDEX;";

        var indexDbParam = indexCmd.CreateParameter();
        indexDbParam.ParameterName = "@dbName";
        indexDbParam.Value = DatabaseName;
        indexCmd.Parameters.Add(indexDbParam);

        var indexParts = new List<(string IndexName, string ColumnName, int Seq, int NonUnique)>();
        await using (var reader = await indexCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexParts.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)
                ));
            }
        }

        indexParts.Should().HaveCount(2);
        indexParts[0].IndexName.Should().Be("IX_CustomerIdentities_Issuer_Subject");
        indexParts[0].ColumnName.Should().Be("Issuer");
        indexParts[0].Seq.Should().Be(1);
        indexParts[0].NonUnique.Should().Be(0); // Unique

        indexParts[1].IndexName.Should().Be("IX_CustomerIdentities_Issuer_Subject");
        indexParts[1].ColumnName.Should().Be("Subject");
        indexParts[1].Seq.Should().Be(2);
        indexParts[1].NonUnique.Should().Be(0); // Unique
    }

    [Fact]
    public async Task ResolveOrCreateAsync_CaseSensitiveSubject_PersistsDistinctRowsAndIds()
    {
        // Arrange
        const string issuer = "https://identity.example.invalid/";
        const string subjectA = "auth0|CaseSensitive";
        const string subjectB = "auth0|casesensitive";

        await using var contextA = CreateDbContext();
        var storeA = new CustomerIdentityStore(contextA);

        await using var contextB = CreateDbContext();
        var storeB = new CustomerIdentityStore(contextB);

        // Act
        var idA = await storeA.ResolveOrCreateAsync(issuer, subjectA, CancellationToken.None);
        var idB = await storeB.ResolveOrCreateAsync(issuer, subjectB, CancellationToken.None);

        // Assert
        idA.Should().NotBeEmpty();
        idB.Should().NotBeEmpty();
        idA.Should().NotBe(idB);

        await using var verifyContext = CreateDbContext();
        var count = await verifyContext.CustomerIdentities.CountAsync(x => x.Issuer == issuer);
        count.Should().Be(2);

        var rowA = await verifyContext.CustomerIdentities.FirstOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subjectA);
        var rowB = await verifyContext.CustomerIdentities.FirstOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subjectB);

        rowA.Should().NotBeNull();
        rowA!.Id.Should().Be(idA);

        rowB.Should().NotBeNull();
        rowB!.Id.Should().Be(idB);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_SameIdentity_IsIdempotentAndReturnsSameId()
    {
        // Arrange
        const string issuer = "https://identity.example.invalid/";
        const string subject = "auth0|idempotent-user-123";

        await using var context1 = CreateDbContext();
        var store1 = new CustomerIdentityStore(context1);

        await using var context2 = CreateDbContext();
        var store2 = new CustomerIdentityStore(context2);

        // Act
        var id1 = await store1.ResolveOrCreateAsync(issuer, subject, CancellationToken.None);
        var id2 = await store2.ResolveOrCreateAsync(issuer, subject, CancellationToken.None);

        // Assert
        id1.Should().NotBeEmpty();
        id2.Should().Be(id1);

        await using var verifyContext = CreateDbContext();
        var rows = await verifyContext.CustomerIdentities
            .Where(x => x.Issuer == issuer && x.Subject == subject)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Id.Should().Be(id1);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_DifferentSubjects_ReturnsDifferentIds()
    {
        // Arrange
        const string issuer = "https://identity.example.invalid/";
        const string subject1 = "auth0|distinct-user-1";
        const string subject2 = "auth0|distinct-user-2";

        await using var context1 = CreateDbContext();
        var store1 = new CustomerIdentityStore(context1);

        await using var context2 = CreateDbContext();
        var store2 = new CustomerIdentityStore(context2);

        // Act
        var id1 = await store1.ResolveOrCreateAsync(issuer, subject1, CancellationToken.None);
        var id2 = await store2.ResolveOrCreateAsync(issuer, subject2, CancellationToken.None);

        // Assert
        id1.Should().NotBeEmpty();
        id2.Should().NotBeEmpty();
        id1.Should().NotBe(id2);

        await using var verifyContext = CreateDbContext();
        var rows = await verifyContext.CustomerIdentities
            .Where(x => x.Issuer == issuer && (x.Subject == subject1 || x.Subject == subject2))
            .ToListAsync();

        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_RealConcurrentCalls_ConvergeToSingleCustomerId()
    {
        // Arrange
        const string issuer = "https://identity.example.invalid/";
        const string subject = "auth0|concurrent-race-subject-999";
        const int concurrentCount = 16;

        using var startBarrier = new Barrier(concurrentCount);
        var tasks = new List<Task<Guid>>();

        for (var i = 0; i < concurrentCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var dbContext = CreateDbContext();
                var store = new CustomerIdentityStore(dbContext);

                // Synchronize all threads so they genuinely race on first insert
                startBarrier.SignalAndWait();

                return await store.ResolveOrCreateAsync(issuer, subject, CancellationToken.None);
            }));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentCount);
        results.All(id => id != Guid.Empty).Should().BeTrue();

        // All concurrent callers must receive the EXACT same CustomerId
        var distinctIds = results.Distinct().ToList();
        distinctIds.Should().HaveCount(1);

        var convergedCustomerId = distinctIds[0];

        // Database must contain EXACTLY one durable row for this (Issuer, Subject)
        await using var verifyContext = CreateDbContext();
        var durableRows = await verifyContext.CustomerIdentities
            .Where(x => x.Issuer == issuer && x.Subject == subject)
            .ToListAsync();

        durableRows.Should().HaveCount(1);
        durableRows[0].Id.Should().Be(convergedCustomerId);
    }
}
