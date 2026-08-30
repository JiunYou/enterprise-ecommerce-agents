using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Identity.Commands.ResolveCustomerIdentity;

public class ResolveCustomerIdentityCommandHandlerTests
{
    private readonly Mock<ICustomerIdentityStore> _customerIdentityStoreMock;
    private readonly ResolveCustomerIdentityCommandHandler _handler;

    public ResolveCustomerIdentityCommandHandlerTests()
    {
        _customerIdentityStoreMock = new Mock<ICustomerIdentityStore>();
        _handler = new ResolveCustomerIdentityCommandHandler(_customerIdentityStoreMock.Object);
    }

    [Fact]
    public async Task Handle_Should_DelegateToCustomerIdentityStore_And_ReturnSuccessWithCustomerId()
    {
        // Arrange
        var issuer = "https://auth.example.com/";
        var subject = "auth0|test-user-123";
        var expectedCustomerId = Guid.NewGuid();

        _customerIdentityStoreMock
            .Setup(x => x.ResolveOrCreateAsync(issuer, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCustomerId);

        var command = new ResolveCustomerIdentityCommand(issuer, subject);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedCustomerId);

        _customerIdentityStoreMock.Verify(
            x => x.ResolveOrCreateAsync(issuer, subject, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
