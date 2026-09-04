using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;

internal sealed class AddItemToCartCommandHandler : ICommandHandler<AddItemToCartCommand, CartResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public AddItemToCartCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartResponse>> Handle(AddItemToCartCommand request, CancellationToken cancellationToken)
    {
        var productId = new ProductId(request.ProductId);
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<CartResponse>(ProductErrors.NotFound);
        }

        if (!product.IsActive)
        {
            return Result.Failure<CartResponse>(ProductErrors.NotActive);
        }

        var order = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);

        if (order is null)
        {
            order = Order.Create(request.CustomerId, product.Currency);
            _orderRepository.Add(order);
        }

        var unitPrice = new Money(product.Price, product.Currency);
        var addItemResult = order.AddItem(productId, unitPrice, request.Quantity);

        if (addItemResult.IsFailure)
        {
            return Result.Failure<CartResponse>(addItemResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var items = order.Items.Select(item => new CartItemResponse(
            item.ProductId.Value,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency,
            item.Quantity,
            item.GetTotalPrice().Amount)).ToList();

        var response = new CartResponse(
            order.Id.Value,
            order.Currency,
            order.TotalAmount.Amount,
            items);

        return Result.Success(response);
    }
}
