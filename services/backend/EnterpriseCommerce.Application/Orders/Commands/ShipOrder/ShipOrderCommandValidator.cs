using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.ShipOrder;

public sealed class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");
    }
}
