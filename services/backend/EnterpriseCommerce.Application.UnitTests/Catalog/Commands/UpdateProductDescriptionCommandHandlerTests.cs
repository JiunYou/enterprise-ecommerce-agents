using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductDescription;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Commands;

public class UpdateProductDescriptionCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateProductDescriptionCommandHandler _handler;

    public UpdateProductDescriptionCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new UpdateProductDescriptionCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidDescription_ReturnsSuccessAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductDescriptionCommand(product.Id, "New Product Description");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be("New Product Description");
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-1");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyDescription_ClearsDescriptionAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.UpdateDescription("Initial description");
        var command = new UpdateProductDescriptionCommand(product.Id, "   ");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be(string.Empty);

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateProductDescriptionCommand(Guid.NewGuid(), "Some Description");

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
        product.UpdateDescription("Initial description");
        var longDescription = new string('A', 2001);
        var command = new UpdateProductDescriptionCommand(product.Id, longDescription);

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidDescription);
        product.Description.Should().Be("Initial description");
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbUpdateConcurrencyExceptionOccurs_ShouldReturnConflictResultAndNotRetry()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductDescriptionCommand(product.Id, "New Description");

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
    public void Validator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var validator = new UpdateProductDescriptionCommandValidator();
        var command = new UpdateProductDescriptionCommand(Guid.NewGuid(), "Valid Description");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithEmptyOrWhitespaceDescription_PassesValidationBecauseItClears()
    {
        // Arrange
        var validator = new UpdateProductDescriptionCommandValidator();
        var commandEmpty = new UpdateProductDescriptionCommand(Guid.NewGuid(), "");
        var commandWhitespace = new UpdateProductDescriptionCommand(Guid.NewGuid(), "   ");

        // Act & Assert
        validator.Validate(commandEmpty).IsValid.Should().BeTrue();
        validator.Validate(commandWhitespace).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithNullDescription_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductDescriptionCommandValidator();
        var command = new UpdateProductDescriptionCommand(Guid.NewGuid(), null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductDescriptionCommand.Description));
    }

    [Fact]
    public void Validator_WithDescriptionExceeding2000_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductDescriptionCommandValidator();
        var command = new UpdateProductDescriptionCommand(Guid.NewGuid(), new string('X', 2001));

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductDescriptionCommand.Description));
    }

    [Fact]
    public void Validator_WithEmptyProductId_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductDescriptionCommandValidator();
        var command = new UpdateProductDescriptionCommand(Guid.Empty, "Valid Description");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductDescriptionCommand.ProductId));
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
