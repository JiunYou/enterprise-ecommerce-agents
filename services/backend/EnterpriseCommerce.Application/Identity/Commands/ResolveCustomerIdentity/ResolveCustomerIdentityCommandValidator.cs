using FluentValidation;

namespace EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;

public class ResolveCustomerIdentityCommandValidator : AbstractValidator<ResolveCustomerIdentityCommand>
{
    public ResolveCustomerIdentityCommandValidator()
    {
        RuleFor(x => x.Issuer)
            .NotEmpty()
            .WithMessage("Issuer is required.")
            .MaximumLength(512)
            .WithMessage("Issuer must not exceed 512 characters.");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required.")
            .MaximumLength(255)
            .WithMessage("Subject must not exceed 255 characters.")
            .Must(subject => string.IsNullOrEmpty(subject) || subject.All(char.IsAscii))
            .WithMessage("Subject must contain only ASCII characters.");
    }
}
