using EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Queries.GetProductBySku;

public class GetProductBySkuQueryValidatorTests
{
    private readonly GetProductBySkuQueryValidator _validator;

    public GetProductBySkuQueryValidatorTests()
    {
        _validator = new GetProductBySkuQueryValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validator_Should_HaveError_When_SkuIsEmpty(string? invalidSku)
    {
        // Arrange
        var query = new GetProductBySkuQuery(invalidSku!);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public void Validator_Should_HaveError_When_SkuExceedsMaxLength()
    {
        // Arrange
        var longSku = new string('A', 101);
        var query = new GetProductBySkuQuery(longSku);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public void Validator_Should_BeValid_When_SkuIsValid()
    {
        // Arrange
        var query = new GetProductBySkuQuery("VALID-SKU-123");

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
