using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductCategory;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Commands;

public class UpdateProductCategoryCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateProductCategoryCommandHandler _handler;

    public UpdateProductCategoryCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new UpdateProductCategoryCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCategory_ReturnsSuccessAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductCategoryCommand(product.Id, "Electronics");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Category.Should().Be("Electronics");
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-1");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyCategory_ClearsCategoryAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.UpdateCategory("Electronics");
        var command = new UpdateProductCategoryCommand(product.Id, "   ");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Category.Should().Be(string.Empty);

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateProductCategoryCommand(Guid.NewGuid(), "Electronics");

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
    public async Task Handle_WithDomainInvalidError_ReturnsDomainError()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.UpdateCategory("Electronics");
        var longCategory = new string('C', 101);
        var command = new UpdateProductCategoryCommand(product.Id, longCategory);

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidCategory);
        product.Category.Should().Be("Electronics");
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbUpdateConcurrencyExceptionOccurs_ShouldReturnConflictResultAndNotRetry()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductCategoryCommand(product.Id, "Electronics");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ConcurrencyConflict);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once, "不應在並發衝突時重試");
    }

    [Fact]
    public void Validator_WithValidCategoryCommand_PassesValidation()
    {
        // Arrange
        var validator = new UpdateProductCategoryCommandValidator();
        var command = new UpdateProductCategoryCommand(Guid.NewGuid(), "Electronics");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithEmptyOrWhitespaceCategory_PassesValidationBecauseItClears()
    {
        // Arrange
        var validator = new UpdateProductCategoryCommandValidator();
        var commandEmpty = new UpdateProductCategoryCommand(Guid.NewGuid(), "");
        var commandWhitespace = new UpdateProductCategoryCommand(Guid.NewGuid(), "   ");

        // Act & Assert
        validator.Validate(commandEmpty).IsValid.Should().BeTrue();
        validator.Validate(commandWhitespace).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithNullCategory_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductCategoryCommandValidator();
        var command = new UpdateProductCategoryCommand(Guid.NewGuid(), null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductCategoryCommand.Category));
    }

    [Fact]
    public void Validator_WithCategoryExceeding100_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductCategoryCommandValidator();
        var longCategory = new string('A', 101);
        var command = new UpdateProductCategoryCommand(Guid.NewGuid(), longCategory);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductCategoryCommand.Category));
    }

    [Fact]
    public void Validator_WithEmptyProductId_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductCategoryCommandValidator();
        var command = new UpdateProductCategoryCommand(Guid.Empty, "Electronics");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductCategoryCommand.ProductId));
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
