using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductPrice;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog;

public class UpdateProductPriceCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateProductPriceCommandHandler _handler;

    public UpdateProductPriceCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new UpdateProductPriceCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductPriceCommand(product.Id, 150m);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Price.Should().Be(150m);
        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsFailure()
    {
        // Arrange
        var command = new UpdateProductPriceCommand(Guid.NewGuid(), 150m);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
    }
}
