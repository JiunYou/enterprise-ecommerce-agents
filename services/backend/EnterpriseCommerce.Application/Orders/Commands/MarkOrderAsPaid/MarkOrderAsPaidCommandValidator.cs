using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid;

public sealed class MarkOrderAsPaidCommandValidator : AbstractValidator<MarkOrderAsPaidCommand>
{
    public MarkOrderAsPaidCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");
    }
}
