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
    public async Task Handle_WithExistingActiveProduct_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var query = new GetProductByIdQuery(product.Id, AllowInactive: false);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(product.Id);
        result.Value.Name.Should().Be("Test Product");
        result.Value.Price.Should().Be(100m);
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
    public async Task Handle_WithInactiveProduct_WhenAllowInactive_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Deactivated Product", "SKU-DEACT", 100m, "TWD").Value;
        product.Deactivate();
        var query = new GetProductByIdQuery(product.Id, AllowInactive: true);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
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
