using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid ProductId) : ICommand;

public sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

internal sealed class DeactivateProductCommandHandler : ICommandHandler<DeactivateProductCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public DeactivateProductCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var deactivateResult = product.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return Result.Failure(deactivateResult.Error);
        }

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
