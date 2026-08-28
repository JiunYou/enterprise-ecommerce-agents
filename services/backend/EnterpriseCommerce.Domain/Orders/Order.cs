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
    
    public bool IsExpired(DateTimeOffset threshold)
    {
        return Status == OrderStatus.Submitted && SubmittedAt.HasValue && SubmittedAt.Value <= threshold;
    }
    
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money TotalAmount => _items.Count == 0 
        ? Money.Zero(Currency) 
        : _items.Select(x => x.GetTotalPrice()).Aggregate((a, b) => a + b);

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

        var orderItem = new OrderItem(Id, productId, unitPrice, quantity);
        _items.Add(orderItem);

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

    public Result Submit(DateTimeOffset submittedAt)
    {
        var result = ChangeStatus(OrderStatus.Submitted);
        if (result.IsSuccess)
        {
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
}
