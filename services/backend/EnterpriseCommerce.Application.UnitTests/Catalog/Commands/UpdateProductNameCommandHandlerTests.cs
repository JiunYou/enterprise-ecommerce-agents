using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductName;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Commands;

public class UpdateProductNameCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateProductNameCommandHandler _handler;

    public UpdateProductNameCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new UpdateProductNameCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsSuccessAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductNameCommand(product.Id, "New Product Name");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Product Name");
        product.Sku.Should().Be("SKU-1");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateProductNameCommand(Guid.NewGuid(), "New Name");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidDomainName_ReturnsDomainError()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductNameCommand(product.Id, "   ");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidName);
        product.Name.Should().Be("Old Name");
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbUpdateConcurrencyExceptionOccurs_ShouldReturnConflictResultAndNotRetry()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductNameCommand(product.Id, "New Name");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ConcurrencyConflict);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once, "Should not retry on concurrency conflict");
    }

    [Fact]
    public void Validator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var validator = new UpdateProductNameCommandValidator();
        var command = new UpdateProductNameCommand(Guid.NewGuid(), "Valid Product Name");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_WithEmptyOrWhitespaceName_FailsValidation(string? invalidName)
    {
        // Arrange
        var validator = new UpdateProductNameCommandValidator();
        var command = new UpdateProductNameCommand(Guid.NewGuid(), invalidName!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductNameCommand.NewName));
    }

    [Fact]
    public void Validator_WithNameExceeding255_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductNameCommandValidator();
        var command = new UpdateProductNameCommand(Guid.NewGuid(), new string('X', 256));

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductNameCommand.NewName));
    }

    [Fact]
    public void Validator_WithEmptyProductId_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductNameCommandValidator();
        var command = new UpdateProductNameCommand(Guid.Empty, "Valid Name");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductNameCommand.ProductId));
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
