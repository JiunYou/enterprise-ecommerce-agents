using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.UpdateProductPrice;

public sealed record UpdateProductPriceCommand(Guid ProductId, decimal NewPrice) : ICommand;

public sealed class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewPrice).GreaterThan(0);
    }
}

internal sealed class UpdateProductPriceCommandHandler : ICommandHandler<UpdateProductPriceCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateProductPriceCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var updateResult = product.UpdatePrice(request.NewPrice);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
        }

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
