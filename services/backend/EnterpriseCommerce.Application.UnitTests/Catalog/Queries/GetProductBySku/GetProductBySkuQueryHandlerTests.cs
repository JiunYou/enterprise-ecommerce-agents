using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Queries.GetProductBySku;

public class GetProductBySkuQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly GetProductBySkuQueryHandler _handler;

    public GetProductBySkuQueryHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductBySkuQueryHandler(_productRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingActiveProduct_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-100", 120m, "TWD").Value;
        var query = new GetProductBySkuQuery("SKU-100", AllowInactive: false);

        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync("SKU-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Sku.Should().Be("SKU-100");
    }

    [Fact]
    public async Task Handle_WithInactiveProduct_WhenNotAllowInactive_ReturnsNotFound()
    {
        // Arrange
        var product = Product.Create("Inactive Product", "SKU-INACTIVE", 120m, "TWD").Value;
        product.Deactivate();
        var query = new GetProductBySkuQuery("SKU-INACTIVE", AllowInactive: false);

        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync("SKU-INACTIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithInactiveProduct_WhenAllowInactive_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Inactive Product", "SKU-INACTIVE", 120m, "TWD").Value;
        product.Deactivate();
        var query = new GetProductBySkuQuery("SKU-INACTIVE", AllowInactive: true);

        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync("SKU-INACTIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsFailure()
    {
        // Arrange
        var query = new GetProductBySkuQuery("NON-EXISTENT-SKU");

        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync("NON-EXISTENT-SKU", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
    }
}
