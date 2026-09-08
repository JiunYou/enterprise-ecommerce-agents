using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly CreateProductCommandHandler _handler;
    private readonly CreateProductCommandValidator _validator;

    public CreateProductCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new CreateProductCommandHandler(
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);
        _validator = new CreateProductCommandValidator();
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesProductAndInventoryItemAtomically()
    {
        // Arrange (A1, A2, A3, A4, A5)
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD", 10);
        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Product? capturedProduct = null;
        _productRepositoryMock.Setup(repo => repo.Add(It.IsAny<Product>()))
            .Callback<Product>(p => capturedProduct = p);

        InventoryItem? capturedInventory = null;
        _inventoryRepositoryMock.Setup(repo => repo.Add(It.IsAny<InventoryItem>()))
            .Callback<InventoryItem>(inv => capturedInventory = inv);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _productRepositoryMock.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Once);
        _inventoryRepositoryMock.Verify(repo => repo.Add(It.IsAny<InventoryItem>()), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedProduct.Should().NotBeNull();
        capturedInventory.Should().NotBeNull();
        capturedInventory!.ProductReference.Value.Should().Be(capturedProduct!.Id);
        capturedInventory.AvailableQuantity.Value.Should().Be(10);
        capturedInventory.ReservedQuantity.Value.Should().Be(0);
    }

    [Fact]
    public void Validator_WhenInitialStockIsZero_ShouldFailValidation()
    {
        // Arrange (A6)
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD", 0);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.InitialStock));
    }

    [Fact]
    public void Validator_WhenInitialStockIsNegative_ShouldFailValidation()
    {
        // Arrange (A7)
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD", -5);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.InitialStock));
    }

    [Fact]
    public async Task Handle_WithExistingSku_AddsNeitherProductNorInventory()
    {
        // Arrange (A9)
        var command = new CreateProductCommand("Test Product", "SKU-1", 100m, "TWD", 10);
        var existingProduct = Product.Create("Existing", "SKU-1", 50m, "TWD").Value;
        
        _productRepositoryMock.Setup(repo => repo.GetBySkuAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProduct);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.SkuAlreadyExists");
        _productRepositoryMock.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Never);
        _inventoryRepositoryMock.Verify(repo => repo.Add(It.IsAny<InventoryItem>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
