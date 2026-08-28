using EnterpriseCommerce.Application.Orders.Commands.CancelOrder;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.CancelOrder;

public class CancelOrderCommandValidatorTests
{
    private readonly CancelOrderCommandValidator _validator;

    public CancelOrderCommandValidatorTests()
    {
        _validator = new CancelOrderCommandValidator();
    }

    [Fact]
    public void Should_Have_Error_When_OrderId_Is_Empty()
    {
        var command = new CancelOrderCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_OrderId_Is_Valid()
    {
        var command = new CancelOrderCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
