using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.DeleteCustomerAddress;
using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Customers.Addresses;

public class DeleteCustomerAddressCommandHandlerTests
{
    private readonly Mock<ICustomerAddressRepository> _repositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly DeleteCustomerAddressCommandHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _addressId = Guid.NewGuid();

    public DeleteCustomerAddressCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICustomerAddressRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();

        _handler = new DeleteCustomerAddressCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenAddressIsOwnedByCustomer_ShouldRemoveAndSaveChangesOnce()
    {
        // Arrange
        var address = CustomerAddress.Create(
            _addressId,
            _customerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            DateTimeOffset.UtcNow).Value;

        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var command = new DeleteCustomerAddressCommand(_customerId, _addressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.Remove(address), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAddressDoesNotExist_ShouldReturnNotFoundAndNotSaveChanges()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var command = new DeleteCustomerAddressCommand(_customerId, _addressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _repositoryMock.Verify(r => r.Remove(It.IsAny<CustomerAddress>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAddressBelongsToAnotherCustomer_ShouldReturnNotFoundThroughCustomerScopedLookup()
    {
        // Arrange: Repository returns null because the query is scoped to _customerId
        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var command = new DeleteCustomerAddressCommand(_customerId, _addressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _repositoryMock.Verify(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.Remove(It.IsAny<CustomerAddress>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
