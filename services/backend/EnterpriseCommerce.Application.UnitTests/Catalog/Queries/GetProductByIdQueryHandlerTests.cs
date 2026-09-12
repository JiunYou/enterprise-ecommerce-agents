using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog;

public class GetProductByIdQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly GetProductByIdQueryHandler _handler;

    public GetProductByIdQueryHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductByIdQueryHandler(_productRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingActiveProduct_ReturnsSuccessWithProductDetailResponseMappingDescription()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.UpdateDescription("A detailed test description");
        product.UpdateImageUrl("https://example.com/image.jpg");
        var query = new GetProductByIdQuery(product.Id, AllowInactive: false);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeOfType<ProductDetailResponse>();
        result.Value.Id.Should().Be(product.Id);
        result.Value.Name.Should().Be("Test Product");
        result.Value.Sku.Should().Be("SKU-1");
        result.Value.Price.Should().Be(100m);
        result.Value.Currency.Should().Be("TWD");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Description.Should().Be("A detailed test description");
        result.Value.ImageUrl.Should().Be("https://example.com/image.jpg");
    }

    [Fact]
    public async Task Handle_WithInactiveProduct_WhenNotAllowInactive_ReturnsNotFound()
    {
        // Arrange
        var product = Product.Create("Deactivated Product", "SKU-DEACT", 100m, "TWD").Value;
        product.Deactivate();
        var query = new GetProductByIdQuery(product.Id, AllowInactive: false);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithInactiveProduct_WhenAllowInactive_ReturnsSuccessWithProductDetailResponseMappingDescription()
    {
        // Arrange
        var product = Product.Create("Deactivated Product", "SKU-DEACT", 100m, "TWD").Value;
        product.UpdateDescription("Inactive product description");
        product.Deactivate();
        var query = new GetProductByIdQuery(product.Id, AllowInactive: true);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeOfType<ProductDetailResponse>();
        result.Value.Id.Should().Be(product.Id);
        result.Value.Name.Should().Be("Deactivated Product");
        result.Value.Sku.Should().Be("SKU-DEACT");
        result.Value.Price.Should().Be(100m);
        result.Value.Currency.Should().Be("TWD");
        result.Value.IsActive.Should().BeFalse();
        result.Value.Description.Should().Be("Inactive product description");
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsFailure()
    {
        // Arrange
        var query = new GetProductByIdQuery(Guid.NewGuid());
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(query.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
    }
}
