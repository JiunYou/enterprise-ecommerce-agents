using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId must not be empty.");
    }
}
