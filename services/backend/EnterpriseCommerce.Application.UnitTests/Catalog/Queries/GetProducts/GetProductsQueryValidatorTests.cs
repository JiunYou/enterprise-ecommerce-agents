using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Catalog.Queries.GetProducts;

public class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _validator;

    public GetProductsQueryValidatorTests()
    {
        _validator = new GetProductsQueryValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_Should_HaveError_When_PageIsZeroOrNegative(int invalidPage)
    {
        // Arrange
        var query = new GetProductsQuery(Page: invalidPage, PageSize: 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validator_Should_HaveError_When_PageSizeIsInvalid(int invalidPageSize)
    {
        // Arrange
        var query = new GetProductsQuery(Page: 1, PageSize: invalidPageSize);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData("invalid_field")]
    [InlineData("category")]
    [InlineData("created_at")]
    public void Validator_Should_HaveError_When_SortByIsUnsupported(string unsupportedSortBy)
    {
        // Arrange
        var query = new GetProductsQuery(SortBy: unsupportedSortBy);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("invalid_order")]
    [InlineData("ascending_wrong")]
    public void Validator_Should_HaveError_When_SortOrderIsUnsupported(string unsupportedSortOrder)
    {
        // Arrange
        var query = new GetProductsQuery(SortOrder: unsupportedSortOrder);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SortOrder);
    }

    [Theory]
    [InlineData(1, 10, null, null)]
    [InlineData(5, 50, "name", "asc")]
    [InlineData(10, 100, "price", "desc")]
    [InlineData(1, 20, "NAME", "DESC")]
    public void Validator_Should_BeValid_When_ParametersAreValid(int page, int pageSize, string? sortBy, string? sortOrder)
    {
        // Arrange
        var query = new GetProductsQuery(Page: page, PageSize: pageSize, SortBy: sortBy, SortOrder: sortOrder);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
