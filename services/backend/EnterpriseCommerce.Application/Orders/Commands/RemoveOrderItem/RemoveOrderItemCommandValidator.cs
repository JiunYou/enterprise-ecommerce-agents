using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem;

public sealed class RemoveOrderItemCommandValidator : AbstractValidator<RemoveOrderItemCommand>
{
    public RemoveOrderItemCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required.");
    }
}
