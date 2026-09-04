using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence.Repositories;

public class CustomerIdentityStoreTests
{
    private readonly DbContextOptions<EnterpriseCommerceDbContext> _dbOptions;
    private readonly EnterpriseCommerceDbContext _dbContext;
    private readonly CustomerIdentityStore _store;

    public CustomerIdentityStoreTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(_dbOptions);
        _store = new CustomerIdentityStore(_dbContext);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldCreateCustomerId_WhenIdentityDoesNotExist()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject = "auth0|user-abc-123";

        // Act
        var customerId = await _store.ResolveOrCreateAsync(issuer, subject);

        // Assert
        customerId.Should().NotBeEmpty();

        var record = await _dbContext.CustomerIdentities.SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject);
        record.Should().NotBeNull();
        record!.Id.Should().Be(customerId);
        record.Issuer.Should().Be(issuer);
        record.Subject.Should().Be(subject);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldReturnSameCustomerId_WhenIdentityAlreadyExists()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject = "auth0|user-abc-123";

        var firstCustomerId = await _store.ResolveOrCreateAsync(issuer, subject);

        // Act
        var secondCustomerId = await _store.ResolveOrCreateAsync(issuer, subject);

        // Assert
        secondCustomerId.Should().Be(firstCustomerId);

        var count = await _dbContext.CustomerIdentities.CountAsync(x => x.Issuer == issuer && x.Subject == subject);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldReturnDifferentCustomerId_ForDifferentSubjects()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject1 = "auth0|user-1";
        var subject2 = "auth0|user-2";

        // Act
        var customerId1 = await _store.ResolveOrCreateAsync(issuer, subject1);
        var customerId2 = await _store.ResolveOrCreateAsync(issuer, subject2);

        // Assert
        customerId1.Should().NotBe(customerId2);

        var total = await _dbContext.CustomerIdentities.CountAsync();
        total.Should().Be(2);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldPreserveOpaqueSubject_WithoutParsingAsGuid()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var nonGuidOpaqueSubject = "google-oauth2|109283746501928374650";

        // Act
        var customerId = await _store.ResolveOrCreateAsync(issuer, nonGuidOpaqueSubject);

        // Assert
        customerId.Should().NotBeEmpty();
        var record = await _dbContext.CustomerIdentities.SingleAsync(x => x.Issuer == issuer && x.Subject == nonGuidOpaqueSubject);
        record.Subject.Should().Be(nonGuidOpaqueSubject);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldAcceptMaxLength255Subject()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject255 = new string('x', 255);

        // Act
        var customerId = await _store.ResolveOrCreateAsync(issuer, subject255);

        // Assert
        customerId.Should().NotBeEmpty();
        var record = await _dbContext.CustomerIdentities.SingleAsync(x => x.Issuer == issuer && x.Subject == subject255);
        record.Subject.Should().Be(subject255);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldReturnSameCustomerId_ForSameCanonicalIssuer()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject = "auth0|user-canonical";

        var id1 = await _store.ResolveOrCreateAsync(issuer, subject);
        var id2 = await _store.ResolveOrCreateAsync(issuer, subject);

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ConcurrentCalls_ShouldReturnSameCustomerId_AndCreateSingleRecord()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject = "auth0|concurrent-user-test";

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var context = new EnterpriseCommerceDbContext(_dbOptions);
            var store = new CustomerIdentityStore(context);
            return await store.ResolveOrCreateAsync(issuer, subject);
        });

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        var distinctIds = results.Distinct().ToList();
        distinctIds.Should().HaveCount(1);

        var count = await _dbContext.CustomerIdentities.CountAsync(x => x.Issuer == issuer && x.Subject == subject);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ShouldDistinguishCaseSensitiveSubjects()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subjectLower = "auth0|UserAbc";
        var subjectUpper = "auth0|USERABC";

        // Act
        var id1 = await _store.ResolveOrCreateAsync(issuer, subjectLower);
        var id2 = await _store.ResolveOrCreateAsync(issuer, subjectUpper);

        // Assert
        id1.Should().NotBe(id2);

        var total = await _dbContext.CustomerIdentities.CountAsync(x => x.Issuer == issuer);
        total.Should().Be(2);
    }
}
