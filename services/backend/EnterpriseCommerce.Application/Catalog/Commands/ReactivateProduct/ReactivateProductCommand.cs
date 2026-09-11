using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.ReactivateProduct;

public sealed record ReactivateProductCommand(Guid ProductId) : ICommand;

public sealed class ReactivateProductCommandValidator : AbstractValidator<ReactivateProductCommand>
{
    public ReactivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

internal sealed class ReactivateProductCommandHandler : ICommandHandler<ReactivateProductCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public ReactivateProductCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var reactivateResult = product.Reactivate();
        if (reactivateResult.IsFailure)
        {
            return Result.Failure(reactivateResult.Error);
        }

        _productRepository.Update(product);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.GetType().FullName?.Contains("DbUpdateConcurrencyException") == true)
        {
            return Result.Failure(ProductErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}
