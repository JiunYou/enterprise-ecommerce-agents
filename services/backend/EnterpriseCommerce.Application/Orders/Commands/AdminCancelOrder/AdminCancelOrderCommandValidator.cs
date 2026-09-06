using FluentValidation;

namespace EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;

/// <summary>
/// 管理員取消訂單命令驗證器。
/// </summary>
public sealed class AdminCancelOrderCommandValidator : AbstractValidator<AdminCancelOrderCommand>
{
    public AdminCancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");

        RuleFor(x => x.ActorIssuer)
            .NotEmpty()
            .WithMessage("ActorIssuer is required.");

        RuleFor(x => x.ActorSubject)
            .NotEmpty()
            .WithMessage("ActorSubject is required.");

        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Reason is required.")
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("Reason cannot be whitespace only.")
            .Must(r => r.Trim().Length <= 500)
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
