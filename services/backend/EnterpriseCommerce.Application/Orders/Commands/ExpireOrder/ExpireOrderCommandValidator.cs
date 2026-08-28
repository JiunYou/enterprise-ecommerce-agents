using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.ExpireOrder;

public sealed class ExpireOrderCommandValidator : AbstractValidator<ExpireOrderCommand>
{
    public ExpireOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
