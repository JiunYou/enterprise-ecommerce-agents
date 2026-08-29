using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryValidatorTests
{
    private readonly GetOrderByIdQueryValidator _validator;

    public GetOrderByIdQueryValidatorTests()
    {
        _validator = new GetOrderByIdQueryValidator();
    }

    [Fact]
    public void Should_Have_Error_When_OrderId_Is_Empty()
    {
        var query = new GetOrderByIdQuery(Guid.Empty, Guid.NewGuid());
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_OrderId_Is_Valid()
    {
        var query = new GetOrderByIdQuery(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.OrderId);
    }
}
