using FluentValidation;

namespace EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;

public class ReleaseInventoryReservationCommandValidator : AbstractValidator<ReleaseInventoryReservationCommand>
{
    public ReleaseInventoryReservationCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required.");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
