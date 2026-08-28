using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Catalog;

public sealed class Product : AggregateRoot<Guid>
{
    private Product()
    {
        Name = string.Empty;
        Sku = string.Empty;
        Currency = string.Empty;
    }

    private Product(Guid id, string name, string sku, decimal price, string currency, bool isActive) : base(id)
    {
        Name = name;
        Sku = sku;
        Price = price;
        Currency = currency;
        IsActive = isActive;
    }

    public string Name { get; private set; }
    public string Sku { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; }
    public bool IsActive { get; private set; }

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
}
