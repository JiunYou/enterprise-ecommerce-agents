using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.UpdateProductName;

public sealed record UpdateProductNameCommand(Guid ProductId, string NewName) : ICommand;

public sealed class UpdateProductNameCommandValidator : AbstractValidator<UpdateProductNameCommand>
{
    public UpdateProductNameCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .Must(name => name.Trim().Length <= 255);
    }
}

internal sealed class UpdateProductNameCommandHandler : ICommandHandler<UpdateProductNameCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateProductNameCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductNameCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var renameResult = product.Rename(request.NewName);
        if (renameResult.IsFailure)
        {
            return Result.Failure(renameResult.Error);
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
