using EnterpriseCommerce.Application.Orders.Commands.AddOrderItem;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.AddOrderItem;

public class AddOrderItemCommandValidatorTests
{
    private readonly AddOrderItemCommandValidator _validator;

    public AddOrderItemCommandValidatorTests()
    {
        _validator = new AddOrderItemCommandValidator();
    }

    [Fact]
    public void Should_Have_Error_When_OrderId_Is_Empty()
    {
        var command = new AddOrderItemCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Fact]
    public void Should_Have_Error_When_ProductId_Is_Empty()
    {
        var command = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public void Should_Have_Error_When_Quantity_Is_Zero_Or_Negative()
    {
        var command = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        var command = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
