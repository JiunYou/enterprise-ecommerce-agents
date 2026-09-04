using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders;

public sealed class OrderItem : Entity<Guid>
{
    public OrderId OrderId { get; private set; } = default!;
    public ProductId ProductId { get; private set; } = default!;
    public Money UnitPrice { get; private set; } = default!;
    public int Quantity { get; private set; }

    internal OrderItem(OrderId orderId, ProductId productId, Money unitPrice, int quantity)
        : base(Guid.NewGuid())
    {
        OrderId = orderId;
        ProductId = productId;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    private OrderItem()
    {
    }

    public Money GetTotalPrice()
    {
        return UnitPrice * Quantity;
    }

    internal void AddQuantity(int additionalQuantity)
    {
        Quantity += additionalQuantity;
    }

    internal void UpdateQuantity(int newQuantity)
    {
        Quantity = newQuantity;
    }
}
