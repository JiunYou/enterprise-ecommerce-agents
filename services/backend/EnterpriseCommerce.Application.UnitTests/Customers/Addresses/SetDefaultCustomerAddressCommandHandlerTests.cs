using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.SetDefaultCustomerAddress;
using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Customers.Addresses;

public class SetDefaultCustomerAddressCommandHandlerTests
{
    private readonly Mock<ICustomerAddressRepository> _repositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly SetDefaultCustomerAddressCommandHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _targetAddressId = Guid.NewGuid();
    private readonly Guid _otherAddressId = Guid.NewGuid();
    private readonly DateTimeOffset _createdAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    public SetDefaultCustomerAddressCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICustomerAddressRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();

        _handler = new SetDefaultCustomerAddressCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTargetBelongsToCustomer_ShouldSetTargetDefault_ClearOthers_AndCommitOnce()
    {
        // Arrange
        var targetAddress = CustomerAddress.Create(
            _targetAddressId,
            _customerId,
            "目標地址",
            "0911111111",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _createdAt).Value;

        var otherAddress = CustomerAddress.Create(
            _otherAddressId,
            _customerId,
            "原有預設地址",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "信義路二段",
            null,
            _createdAt).Value;
        otherAddress.SetAsDefault();

        var customerAddresses = new List<CustomerAddress> { targetAddress, otherAddress };

        _repositoryMock
            .Setup(r => r.GetByCustomerForUpdateAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerAddresses);

        var command = new SetDefaultCustomerAddressCommand(_customerId, _targetAddressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_targetAddressId);
        result.Value.IsDefault.Should().BeTrue();

        targetAddress.IsDefault.Should().BeTrue();
        otherAddress.IsDefault.Should().BeFalse();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetAlreadyDefault_ShouldSucceedAndRemainIdempotent()
    {
        // Arrange
        var targetAddress = CustomerAddress.Create(
            _targetAddressId,
            _customerId,
            "目標地址",
            "0911111111",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _createdAt).Value;
        targetAddress.SetAsDefault();

        var otherAddress = CustomerAddress.Create(
            _otherAddressId,
            _customerId,
            "一般地址",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "信義路二段",
            null,
            _createdAt).Value;

        var customerAddresses = new List<CustomerAddress> { targetAddress, otherAddress };

        _repositoryMock
            .Setup(r => r.GetByCustomerForUpdateAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerAddresses);

        var command = new SetDefaultCustomerAddressCommand(_customerId, _targetAddressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_targetAddressId);
        result.Value.IsDefault.Should().BeTrue();

        targetAddress.IsDefault.Should().BeTrue();
        otherAddress.IsDefault.Should().BeFalse();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUnknownOwnAddress_ShouldRollbackAndReturnNotFound()
    {
        // Arrange
        var otherAddress = CustomerAddress.Create(
            _otherAddressId,
            _customerId,
            "現有地址",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "信義路二段",
            null,
            _createdAt).Value;

        var customerAddresses = new List<CustomerAddress> { otherAddress };

        _repositoryMock
            .Setup(r => r.GetByCustomerForUpdateAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerAddresses);

        var nonExistentAddressId = Guid.NewGuid();
        var command = new SetDefaultCustomerAddressCommand(_customerId, nonExistentAddressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCrossCustomerAddressAbsentFromLockedSet_ShouldRollbackAndReturnNotFound()
    {
        // Arrange: 顧客自己的地址清單中並不包含 crossCustomerAddressId
        var ownAddress = CustomerAddress.Create(
            _otherAddressId,
            _customerId,
            "自己的地址",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "信義路二段",
            null,
            _createdAt).Value;

        var customerAddresses = new List<CustomerAddress> { ownAddress };

        _repositoryMock
            .Setup(r => r.GetByCustomerForUpdateAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerAddresses);

        var crossCustomerAddressId = Guid.NewGuid();
        var command = new SetDefaultCustomerAddressCommand(_customerId, crossCustomerAddressId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDatabaseThrowsException_ShouldRollbackAndRethrow()
    {
        // Arrange
        var targetAddress = CustomerAddress.Create(
            _targetAddressId,
            _customerId,
            "目標地址",
            "0911111111",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _createdAt).Value;

        var customerAddresses = new List<CustomerAddress> { targetAddress };

        _repositoryMock
            .Setup(r => r.GetByCustomerForUpdateAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerAddresses);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB Deadlock / Concurrency Failure"));

        var command = new SetDefaultCustomerAddressCommand(_customerId, _targetAddressId);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB Deadlock / Concurrency Failure");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
