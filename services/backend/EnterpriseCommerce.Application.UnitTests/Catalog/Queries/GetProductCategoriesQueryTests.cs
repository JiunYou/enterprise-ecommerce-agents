using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductCategories;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Queries;

public class GetProductCategoriesQueryTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly GetProductCategoriesQueryHandler _handler;

    public GetProductCategoriesQueryTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductCategoriesQueryHandler(_productRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsDistinctSortedCategoriesFromRepository()
    {
        // Arrange
        var expectedCategories = new List<string> { "Books", "Clothing", "Electronics" };
        _productRepositoryMock
            .Setup(repo => repo.GetActiveCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCategories);

        var query = new GetProductCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal("Books", "Clothing", "Electronics");
        _productRepositoryMock.Verify(repo => repo.GetActiveCategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoActiveCategoriesExist_ReturnsEmptyList()
    {
        // Arrange
        _productRepositoryMock
            .Setup(repo => repo.GetActiveCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var query = new GetProductCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
