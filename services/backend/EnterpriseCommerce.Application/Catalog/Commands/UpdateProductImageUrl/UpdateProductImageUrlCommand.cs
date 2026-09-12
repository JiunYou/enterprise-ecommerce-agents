using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Commands.UpdateProductImageUrl;

public sealed record UpdateProductImageUrlCommand(Guid ProductId, string ImageUrl) : ICommand;

public sealed class UpdateProductImageUrlCommandValidator : AbstractValidator<UpdateProductImageUrlCommand>
{
    public UpdateProductImageUrlCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.ImageUrl)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(url => url.Trim().Length <= 2048)
            .Must(BeAValidHttpsUrlOrEmpty);
    }

    private static bool BeAValidHttpsUrlOrEmpty(string imageUrl)
    {
        var trimmed = imageUrl.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return true;
    }
}

internal sealed class UpdateProductImageUrlCommandHandler : ICommandHandler<UpdateProductImageUrlCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateProductImageUrlCommandHandler(IProductRepository productRepository, IApplicationUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductImageUrlCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        var updateResult = product.UpdateImageUrl(request.ImageUrl);
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
