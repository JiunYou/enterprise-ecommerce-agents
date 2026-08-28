using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
