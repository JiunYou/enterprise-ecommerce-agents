using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;

public sealed record CreateProductCommand(string Name, string Sku, decimal Price, string Currency) : ICommand<Guid>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

internal sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var existingProduct = await _productRepository.GetBySkuAsync(request.Sku, cancellationToken);
        if (existingProduct != null)
        {
            return Result.Failure<Guid>(new Error("Product.SkuAlreadyExists", "A product with the specified SKU already exists."));
        }

        var productResult = Product.Create(request.Name, request.Sku, request.Price, request.Currency);
        if (productResult.IsFailure)
        {
            return Result.Failure<Guid>(productResult.Error);
        }

        _productRepository.Add(productResult.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(productResult.Value.Id);
    }
}
