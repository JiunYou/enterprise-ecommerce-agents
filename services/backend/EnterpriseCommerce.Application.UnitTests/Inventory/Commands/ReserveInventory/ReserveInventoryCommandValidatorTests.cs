using EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;

namespace EnterpriseCommerce.Application.UnitTests.Inventory.Commands.ReserveInventory;

public class ReserveInventoryCommandValidatorTests
{
    private readonly ReserveInventoryCommandValidator _validator;

    public ReserveInventoryCommandValidatorTests()
    {
        _validator = new ReserveInventoryCommandValidator();
    }

    [Fact]
    public void Validator_Should_HaveError_When_ProductIdIsEmpty()
    {
        var command = new ReserveInventoryCommand(Guid.Empty, Guid.NewGuid(), 5);
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "ProductId");
    }

    [Fact]
    public void Validator_Should_HaveError_When_QuantityIsZeroOrNegative()
    {
        var commandZero = new ReserveInventoryCommand(Guid.NewGuid(), Guid.NewGuid(), 0);
        var resultZero = _validator.Validate(commandZero);

        Assert.False(resultZero.IsValid);
        Assert.Contains(resultZero.Errors, x => x.PropertyName == "Quantity");

        var commandNegative = new ReserveInventoryCommand(Guid.NewGuid(), Guid.NewGuid(), -1);
        var resultNegative = _validator.Validate(commandNegative);

        Assert.False(resultNegative.IsValid);
        Assert.Contains(resultNegative.Errors, x => x.PropertyName == "Quantity");
    }

    [Fact]
    public void Validator_Should_BeValid_When_CommandIsValid()
    {
        var command = new ReserveInventoryCommand(Guid.NewGuid(), Guid.NewGuid(), 5);
        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
