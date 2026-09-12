using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Catalog;

public sealed class Product : AggregateRoot<Guid>
{
    private Product()
    {
        Name = string.Empty;
        Sku = string.Empty;
        Currency = string.Empty;
        Description = string.Empty;
        ImageUrl = string.Empty;
    }

    private Product(Guid id, string name, string sku, decimal price, string currency, bool isActive) : base(id)
    {
        Name = name;
        Sku = sku;
        Price = price;
        Currency = currency;
        IsActive = isActive;
        Description = string.Empty;
        ImageUrl = string.Empty;
    }

    public string Name { get; private set; }
    public string Sku { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; }
    public bool IsActive { get; private set; }
    public string Description { get; private set; }
    public string ImageUrl { get; private set; }

    public static Result<Product> Create(string name, string sku, decimal price, string currency)
    {
        if (price <= 0)
        {
            return Result.Failure<Product>(ProductErrors.InvalidPrice);
        }

        var product = new Product(Guid.NewGuid(), name, sku, price, currency, true);
        
        return Result.Success(product);
    }

    public Result UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
        {
            return Result.Failure(ProductErrors.InvalidPrice);
        }

        Price = newPrice;
        
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure(ProductErrors.AlreadyDeactivated);
        }

        IsActive = false;
        
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(ProductErrors.AlreadyActive);
        }

        IsActive = true;

        return Result.Success();
    }

    public Result Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Failure(ProductErrors.InvalidName);
        }

        var trimmedName = newName.Trim();
        if (trimmedName.Length > 255)
        {
            return Result.Failure(ProductErrors.InvalidName);
        }

        Name = trimmedName;

        return Result.Success();
    }

    public Result UpdateDescription(string description)
    {
        if (description is null)
        {
            return Result.Failure(ProductErrors.InvalidDescription);
        }

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > 2000)
        {
            return Result.Failure(ProductErrors.InvalidDescription);
        }

        Description = trimmedDescription;

        return Result.Success();
    }

    public Result UpdateImageUrl(string imageUrl)
    {
        if (imageUrl is null)
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        var trimmedImageUrl = imageUrl.Trim();
        if (trimmedImageUrl.Length == 0)
        {
            ImageUrl = string.Empty;
            return Result.Success();
        }

        if (trimmedImageUrl.Length > 2048)
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        if (!Uri.TryCreate(trimmedImageUrl, UriKind.Absolute, out var uri))
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Result.Failure(ProductErrors.InvalidImageUrl);
        }

        ImageUrl = trimmedImageUrl;

        return Result.Success();
    }
}
