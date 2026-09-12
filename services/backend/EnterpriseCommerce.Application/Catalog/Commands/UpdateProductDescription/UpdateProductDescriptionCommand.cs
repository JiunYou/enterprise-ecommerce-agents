using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.UpdateProductDescription;

public sealed record UpdateProductDescriptionCommand(Guid ProductId, string Description) : ICommand;

public sealed class UpdateProductDescriptionCommandValidator : AbstractValidator<UpdateProductDescriptionCommand>
{
    public UpdateProductDescriptionCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Description)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(desc => desc.Trim().Length <= 2000);
    }
}

internal sealed class UpdateProductDescriptionCommandHandler : ICommandHandler<UpdateProductDescriptionCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateProductDescriptionCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductDescriptionCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var updateResult = product.UpdateDescription(request.Description);
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
