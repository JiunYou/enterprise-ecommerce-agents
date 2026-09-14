using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.CreateCustomerAddress;
using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Customers.Addresses;

public class CreateCustomerAddressCommandHandlerTests
{
    private readonly Mock<ICustomerAddressRepository> _repositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateCustomerAddressCommandHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    public CreateCustomerAddressCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICustomerAddressRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _handler = new CreateCustomerAddressCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePersistAndSaveChangesOnce()
    {
        // Arrange
        var command = new CreateCustomerAddressCommand(
            _customerId,
            "王小明",
            "0912345678",
            "tw",
            "100",
            "台北市",
            "中正區忠孝西路一段",
            "3 樓之 1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.RecipientName.Should().Be("王小明");
        result.Value.Phone.Should().Be("0912345678");
        result.Value.CountryCode.Should().Be("TW");
        result.Value.PostalCode.Should().Be("100");
        result.Value.City.Should().Be("台北市");
        result.Value.AddressLine1.Should().Be("中正區忠孝西路一段");
        result.Value.AddressLine2.Should().Be("3 樓之 1");

        _repositoryMock.Verify(r => r.Add(It.Is<CustomerAddress>(a =>
            a.CustomerId == _customerId &&
            a.RecipientName == "王小明" &&
            a.Phone == "0912345678" &&
            a.CountryCode == "TW" &&
            a.PostalCode == "100" &&
            a.City == "台北市" &&
            a.AddressLine1 == "中正區忠孝西路一段" &&
            a.AddressLine2 == "3 樓之 1" &&
            a.CreatedAt == _fixedTime)), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidDomainInput_ShouldFailAndNotSaveChanges()
    {
        // Arrange
        var command = new CreateCustomerAddressCommand(
            _customerId,
            "", // Invalid empty recipient name
            "0912345678",
            "TW",
            "100",
            "台北市",
            "中正區忠孝西路一段",
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidRecipientName);

        _repositoryMock.Verify(r => r.Add(It.IsAny<CustomerAddress>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
