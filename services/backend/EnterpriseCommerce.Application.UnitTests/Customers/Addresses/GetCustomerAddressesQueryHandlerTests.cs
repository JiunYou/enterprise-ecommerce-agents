using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Queries.GetCustomerAddresses;
using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Customers.Addresses;

public class GetCustomerAddressesQueryHandlerTests
{
    private readonly Mock<ICustomerAddressRepository> _repositoryMock;
    private readonly GetCustomerAddressesQueryHandler _handler;
    private readonly Guid _customerId = Guid.NewGuid();

    public GetCustomerAddressesQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICustomerAddressRepository>();
        _handler = new GetCustomerAddressesQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasNoAddresses_ShouldReturnEmptyList()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByCustomerAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerAddress>());

        var query = new GetCustomerAddressesQuery(_customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _repositoryMock.Verify(r => r.GetByCustomerAsync(_customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasAddresses_ShouldReturnMappedResponsesWithoutCustomerId()
    {
        // Arrange
        var addr1 = CustomerAddress.Create(
            Guid.NewGuid(),
            _customerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "地址 1",
            "樓層 1",
            DateTimeOffset.UtcNow).Value;

        var addr2 = CustomerAddress.Create(
            Guid.NewGuid(),
            _customerId,
            "李小美",
            "0987654321",
            "TW",
            "200",
            "基隆市",
            "地址 2",
            null,
            DateTimeOffset.UtcNow.AddMinutes(-5)).Value;

        _repositoryMock
            .Setup(r => r.GetByCustomerAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerAddress> { addr1, addr2 });

        var query = new GetCustomerAddressesQuery(_customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var first = result.Value[0];
        first.Id.Should().Be(addr1.Id);
        first.RecipientName.Should().Be("王小明");
        first.Phone.Should().Be("0912345678");
        first.CountryCode.Should().Be("TW");
        first.PostalCode.Should().Be("100");
        first.City.Should().Be("台北市");
        first.AddressLine1.Should().Be("地址 1");
        first.AddressLine2.Should().Be("樓層 1");

        // CustomerAddressResponse 類別定義上即不包含 CustomerId
        typeof(CustomerAddressResponse).GetProperty("CustomerId").Should().BeNull();
    }
}
