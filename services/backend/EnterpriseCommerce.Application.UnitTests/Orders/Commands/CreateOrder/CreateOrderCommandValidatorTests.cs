using EnterpriseCommerce.Application.Orders.Commands.CreateOrder;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.CreateOrder;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator;

    public CreateOrderCommandValidatorTests()
    {
        _validator = new CreateOrderCommandValidator();
    }

    [Fact]
    public void Validator_Should_HaveError_When_CustomerIdIsEmpty()
    {
        var command = new CreateOrderCommand(Guid.Empty, "TWD");
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "CustomerId");
    }

    [Fact]
    public void Validator_Should_HaveError_When_CurrencyIsInvalid()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), "TW");
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "Currency");
    }

    [Fact]
    public void Validator_Should_BeValid_When_CommandIsValid()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), "TWD");
        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
