using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.UpdateCustomerAddress;
using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Customers.Addresses;

public class UpdateCustomerAddressCommandHandlerTests
{
    private readonly Mock<ICustomerAddressRepository> _repositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateCustomerAddressCommandHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _addressId = Guid.NewGuid();
    private readonly DateTimeOffset _createdAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    public UpdateCustomerAddressCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICustomerAddressRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();

        _handler = new UpdateCustomerAddressCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenAddressIsOwnedByCustomer_ShouldUpdateAndSaveChangesOnce()
    {
        // Arrange
        var address = CustomerAddress.Create(
            _addressId,
            _customerId,
            "原姓名",
            "0911111111",
            "TW",
            "100",
            "台北市",
            "原忠孝西路一段",
            null,
            _createdAt).Value;

        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var command = new UpdateCustomerAddressCommand(
            _customerId,
            _addressId,
            "  新姓名  ",
            "  0922222222  ",
            "us",
            " 94105 ",
            " San Francisco ",
            " Market St ",
            " Suite 100 ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(_addressId);
        result.Value.RecipientName.Should().Be("新姓名");
        result.Value.Phone.Should().Be("0922222222");
        result.Value.CountryCode.Should().Be("US");
        result.Value.PostalCode.Should().Be("94105");
        result.Value.City.Should().Be("San Francisco");
        result.Value.AddressLine1.Should().Be("Market St");
        result.Value.AddressLine2.Should().Be("Suite 100");

        // 驗證實體不變量
        address.Id.Should().Be(_addressId);
        address.CustomerId.Should().Be(_customerId);
        address.CreatedAt.Should().Be(_createdAt);

        // 驗證 Repository 透過 customer-scoped 查詢
        _repositoryMock.Verify(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()), Times.Once);

        // 驗證 SaveChangesAsync 正好呼叫一次
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAddressDoesNotExist_ShouldReturnNotFoundAndNotSaveChanges()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var command = new UpdateCustomerAddressCommand(
            _customerId,
            _addressId,
            "新姓名",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "新地址",
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _repositoryMock.Verify(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAddressBelongsToAnotherCustomer_ShouldReturnNotFoundThroughCustomerScopedLookupAndNotSaveChanges()
    {
        // Arrange: Repository 返回 null 因為是用呼叫者的 _customerId 查詢
        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var command = new UpdateCustomerAddressCommand(
            _customerId,
            _addressId,
            "新姓名",
            "0922222222",
            "TW",
            "100",
            "台北市",
            "新地址",
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.NotFound);

        _repositoryMock.Verify(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDomainUpdateFails_ShouldReturnDomainErrorAndNotSaveChanges()
    {
        // Arrange
        var address = CustomerAddress.Create(
            _addressId,
            _customerId,
            "原姓名",
            "0911111111",
            "TW",
            "100",
            "台北市",
            "原地址",
            null,
            _createdAt).Value;

        _repositoryMock
            .Setup(r => r.GetByIdForCustomerAsync(_customerId, _addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        // Act: 提供無效的電話號碼
        var command = new UpdateCustomerAddressCommand(
            _customerId,
            _addressId,
            "新姓名",
            "0912\t345678",
            "TW",
            "100",
            "台北市",
            "新地址",
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPhone);

        // 實體未被變更
        address.RecipientName.Should().Be("原姓名");
        address.Phone.Should().Be("0911111111");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
