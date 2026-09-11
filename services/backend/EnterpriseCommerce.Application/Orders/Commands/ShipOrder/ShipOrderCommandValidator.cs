using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.ShipOrder;

public sealed class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");

        RuleFor(x => x.Carrier)
            .NotEmpty()
            .WithMessage("Carrier is required.")
            .MaximumLength(100)
            .WithMessage("Carrier must not exceed 100 characters.");

        RuleFor(x => x.TrackingNumber)
            .NotEmpty()
            .WithMessage("Tracking number is required.")
            .MaximumLength(100)
            .WithMessage("Tracking number must not exceed 100 characters.");
    }
}
