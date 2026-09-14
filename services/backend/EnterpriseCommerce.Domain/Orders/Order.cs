using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public ShippingAddress? ShippingAddress { get; private set; }
    public string? ShippingCarrier { get; private set; }
    public string? ShippingTrackingNumber { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    
    public bool IsExpired(DateTimeOffset threshold)
    {
        return Status == OrderStatus.Submitted && SubmittedAt.HasValue && SubmittedAt.Value <= threshold;
    }
    
    public string? AppliedCouponCode { get; private set; }
    public decimal? AppliedCouponDiscountAmount { get; private set; }
    public DateTimeOffset? AppliedCouponExpiresAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money SubtotalAmount => _items.Count == 0
        ? Money.Zero(Currency)
        : _items.Select(x => x.GetTotalPrice()).Aggregate((a, b) => a + b);

    public Money DiscountAmount => AppliedCouponDiscountAmount.HasValue
        ? new Money(AppliedCouponDiscountAmount.Value, Currency)
        : Money.Zero(Currency);

    public Money TotalAmount => _items.Count == 0
        ? Money.Zero(Currency)
        : new Money(SubtotalAmount.Amount - DiscountAmount.Amount, Currency);

    public Result ApplyCoupon(string code, Money discount, DateTimeOffset expiresAt)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(OrderErrors.InvalidCouponCode);
        }

        if (discount.Currency != Currency)
        {
            return Result.Failure(OrderErrors.CurrencyMismatch);
        }

        if (discount.Amount <= 0)
        {
            return Result.Failure(OrderErrors.InvalidCouponDiscount);
        }

        if (discount.Amount >= SubtotalAmount.Amount)
        {
            return Result.Failure(OrderErrors.CouponDiscountExceedsSubtotal);
        }

        if (!string.IsNullOrEmpty(AppliedCouponCode))
        {
            return Result.Failure(OrderErrors.CouponAlreadyApplied);
        }

        AppliedCouponCode = code.Trim().ToUpperInvariant();
        AppliedCouponDiscountAmount = discount.Amount;
        AppliedCouponExpiresAt = expiresAt;

        return Result.Success();
    }

    public Result RemoveCoupon()
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        ClearCouponSnapshot();
        return Result.Success();
    }

    private void ClearCouponSnapshot()
    {
        AppliedCouponCode = null;
        AppliedCouponDiscountAmount = null;
        AppliedCouponExpiresAt = null;
    }

    private Order(OrderId id, Guid customerId, string currency) : base(id)
    {
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        Currency = currency;
        // Test 1: Protected Write
    }

    private Order()
    {
    }

    public static Order Create(Guid customerId, string currency)
    {
        var order = new Order(new OrderId(Guid.NewGuid()), customerId, currency);
        order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id, customerId));
        return order;
    }

    public Result AddItem(ProductId productId, Money unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        if (quantity <= 0)
        {
            return Result.Failure(OrderErrors.InvalidQuantity);
        }

        if (unitPrice.Currency != Currency)
        {
            return Result.Failure(OrderErrors.CurrencyMismatch);
        }

        var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.AddQuantity(quantity);
            ClearCouponSnapshot();
            return Result.Success();
        }

        var orderItem = new OrderItem(Id, productId, unitPrice, quantity);
        _items.Add(orderItem);
        ClearCouponSnapshot();

        return Result.Success();
    }

    public Result UpdateItemQuantity(ProductId productId, int quantity)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        if (quantity <= 0)
        {
            return Result.Failure(OrderErrors.InvalidQuantity);
        }

        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item is null)
        {
            return Result.Failure(OrderErrors.ItemNotFound);
        }

        item.UpdateQuantity(quantity);
        ClearCouponSnapshot();
        return Result.Success();
    }

    public Result RemoveItem(ProductId productId)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item is null)
        {
            return Result.Failure(OrderErrors.ItemNotFound);
        }

        _items.Remove(item);
        ClearCouponSnapshot();
        return Result.Success();
    }

    public Result ChangeStatus(OrderStatus newStatus)
    {
        if (_items.Count == 0 && newStatus != OrderStatus.Cancelled)
        {
            return Result.Failure(OrderErrors.EmptyOrder);
        }

        bool isValid = (Status, newStatus) switch
        {
            (OrderStatus.Pending, OrderStatus.Submitted) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Submitted, OrderStatus.Paid) => true,
            (OrderStatus.Submitted, OrderStatus.Cancelled) => true,
            (OrderStatus.Paid, OrderStatus.Shipped) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!isValid)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        var oldStatus = Status;
        Status = newStatus;

        RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id, oldStatus, newStatus));

        return Result.Success();
    }

    public Result Cancel()
    {
        return ChangeStatus(OrderStatus.Cancelled);
    }

    public Result Submit(ShippingAddress shippingAddress, DateTimeOffset submittedAt)
    {
        if (shippingAddress is null)
        {
            return Result.Failure(OrderErrors.ShippingAddressRequired);
        }

        if (AppliedCouponExpiresAt.HasValue && AppliedCouponExpiresAt.Value <= submittedAt)
        {
            return Result.Failure(OrderErrors.AppliedCouponExpired);
        }

        var result = ChangeStatus(OrderStatus.Submitted);
        if (result.IsSuccess)
        {
            ShippingAddress = shippingAddress;
            SubmittedAt = submittedAt;
        }
        return result;
    }


    public Result MarkAsPaid()
    {
        return ChangeStatus(OrderStatus.Paid);
    }

    public Result Ship()
    {
        return ChangeStatus(OrderStatus.Shipped);
    }

    public Result Ship(string carrier, string trackingNumber, DateTimeOffset shippedAt)
    {
        if (Status != OrderStatus.Paid)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        if (ShippingAddress is null)
        {
            return Result.Failure(OrderErrors.ShippingAddressRequired);
        }

        if (string.IsNullOrWhiteSpace(carrier))
        {
            return Result.Failure(OrderErrors.InvalidShippingCarrier);
        }

        var trimmedCarrier = carrier.Trim();
        if (trimmedCarrier.Length > 100 || trimmedCarrier.Any(char.IsControl))
        {
            return Result.Failure(OrderErrors.InvalidShippingCarrier);
        }

        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return Result.Failure(OrderErrors.InvalidShippingTrackingNumber);
        }

        var trimmedTrackingNumber = trackingNumber.Trim();
        if (trimmedTrackingNumber.Length > 100 || trimmedTrackingNumber.Any(char.IsControl))
        {
            return Result.Failure(OrderErrors.InvalidShippingTrackingNumber);
        }

        var result = ChangeStatus(OrderStatus.Shipped);
        if (result.IsFailure)
        {
            return result;
        }

        ShippingCarrier = trimmedCarrier;
        ShippingTrackingNumber = trimmedTrackingNumber;
        ShippedAt = shippedAt;

        return Result.Success();
    }
}
