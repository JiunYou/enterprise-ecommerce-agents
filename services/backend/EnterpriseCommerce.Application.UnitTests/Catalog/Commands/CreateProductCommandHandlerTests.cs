using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new CreateProductCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD");
        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        if (result.IsFailure)
        {
            throw new Exception($"Result failed with error: {result.Error.Code} - {result.Error.Message}");
        }
        result.IsSuccess.Should().BeTrue();
        _productRepositoryMock.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingSku_ReturnsFailure()
    {
        // Arrange
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD");
        var existingProduct = Product.Create("Existing", "SKU-1", 50m, "TWD").Value;
        
        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProduct);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.SkuAlreadyExists");
        _productRepositoryMock.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Never);
    }
}
