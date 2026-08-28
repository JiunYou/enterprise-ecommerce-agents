using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
using EnterpriseCommerce.Domain.Catalog;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Queries.GetProducts;

public class GetProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly GetProductsQueryHandler _handler;

    public GetProductsQueryHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductsQueryHandler(_productRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidPagination_ReturnsPagedProducts()
    {
        // Arrange
        var product1 = Product.Create("Product 1", "SKU-1", 100m, "TWD").Value;
        var product2 = Product.Create("Product 2", "SKU-2", 200m, "TWD").Value;
        var products = new List<Product> { product1, product2 };
        int totalCount = 25;

        var query = new GetProductsQuery(Page: 2, PageSize: 2, OnlyActive: true, SearchTerm: "Product", SortBy: "price", SortOrder: "desc");

        _productRepositoryMock
            .Setup(repo => repo.GetPagedAsync(2, 2, true, "Product", "price", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((products, totalCount));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        result.Value.TotalCount.Should().Be(25);
        result.Value.TotalPages.Should().Be(13);
        result.Value.HasPreviousPage.Should().BeTrue();
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Sku.Should().Be("SKU-1");
        result.Value.Items[1].Sku.Should().Be("SKU-2");
    }

    [Fact]
    public async Task Handle_WhenNoProductsFound_ReturnsEmptyPagedList()
    {
        // Arrange
        var products = new List<Product>();
        int totalCount = 0;

        var query = new GetProductsQuery(Page: 1, PageSize: 10, OnlyActive: true);

        _productRepositoryMock
            .Setup(repo => repo.GetPagedAsync(1, 10, true, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((products, totalCount));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalPages.Should().Be(0);
        result.Value.HasPreviousPage.Should().BeFalse();
        result.Value.HasNextPage.Should().BeFalse();
    }
}
