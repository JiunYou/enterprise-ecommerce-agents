using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.DeactivateProduct;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog;

public class DeactivateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly DeactivateProductCommandHandler _handler;

    public DeactivateProductCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new DeactivateProductCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithActiveProduct_ReturnsSuccess()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new DeactivateProductCommand(product.Id);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAlreadyDeactivatedProduct_ReturnsFailure()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.Deactivate(); // Deactivate it first
        var command = new DeactivateProductCommand(product.Id);
        
        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.AlreadyDeactivated);
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
    }
}
