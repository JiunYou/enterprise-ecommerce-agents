using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.UpdateProductCategory;

public sealed record UpdateProductCategoryCommand(Guid ProductId, string Category) : ICommand;

public sealed class UpdateProductCategoryCommandValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.Category)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(cat => cat.Trim().Length <= 100);
    }
}

internal sealed class UpdateProductCategoryCommandHandler : ICommandHandler<UpdateProductCategoryCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateProductCategoryCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var updateResult = product.UpdateCategory(request.Category);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
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
