using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductImageUrl;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Commands;

public class UpdateProductImageUrlCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly UpdateProductImageUrlCommandHandler _handler;

    public UpdateProductImageUrlCommandHandlerTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new UpdateProductImageUrlCommandHandler(_productRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidHttpsImageUrl_ReturnsSuccessAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductImageUrlCommand(product.Id, "https://example.com/images/prod.png");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.ImageUrl.Should().Be("https://example.com/images/prod.png");
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-1");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyImageUrl_ClearsImageUrlAndCallsSaveOnce()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        product.UpdateImageUrl("https://example.com/images/prod.png");
        var command = new UpdateProductImageUrlCommand(product.Id, "   ");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.ImageUrl.Should().Be(string.Empty);

        _productRepositoryMock.Verify(repo => repo.Update(product), Times.Once);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingProduct_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateProductImageUrlCommand(Guid.NewGuid(), "https://example.com/images/prod.png");

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
        var command = new UpdateProductImageUrlCommand(product.Id, "http://insecure.example.com/a.jpg");

        _productRepositoryMock.Setup(repo => repo.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidImageUrl);
        _productRepositoryMock.Verify(repo => repo.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbUpdateConcurrencyExceptionOccurs_ShouldReturnConflictResultAndNotRetry()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-1", 100m, "TWD").Value;
        var command = new UpdateProductImageUrlCommand(product.Id, "https://example.com/image.jpg");

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
    public void Validator_WithValidHttpsCommand_PassesValidation()
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var command = new UpdateProductImageUrlCommand(Guid.NewGuid(), "https://cdn.example.com/item.png");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithEmptyOrWhitespaceImageUrl_PassesValidationBecauseItClears()
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var commandEmpty = new UpdateProductImageUrlCommand(Guid.NewGuid(), "");
        var commandWhitespace = new UpdateProductImageUrlCommand(Guid.NewGuid(), "   ");

        // Act & Assert
        validator.Validate(commandEmpty).IsValid.Should().BeTrue();
        validator.Validate(commandWhitespace).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithNullImageUrl_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var command = new UpdateProductImageUrlCommand(Guid.NewGuid(), null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductImageUrlCommand.ImageUrl));
    }

    [Fact]
    public void Validator_WithImageUrlExceeding2048_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var longUrl = "https://example.com/" + new string('a', 2040);
        var command = new UpdateProductImageUrlCommand(Guid.NewGuid(), longUrl);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductImageUrlCommand.ImageUrl));
    }

    [Theory]
    [InlineData("http://example.com/image.jpg")]
    [InlineData("ftp://example.com/image.jpg")]
    [InlineData("/relative/path.jpg")]
    [InlineData("https://user:pass@example.com/image.jpg")]
    public void Validator_WithInvalidUriContract_FailsValidation(string invalidUrl)
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var command = new UpdateProductImageUrlCommand(Guid.NewGuid(), invalidUrl);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductImageUrlCommand.ImageUrl));
    }

    [Fact]
    public void Validator_WithEmptyProductId_FailsValidation()
    {
        // Arrange
        var validator = new UpdateProductImageUrlCommandValidator();
        var command = new UpdateProductImageUrlCommand(Guid.Empty, "https://example.com/image.jpg");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductImageUrlCommand.ProductId));
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
