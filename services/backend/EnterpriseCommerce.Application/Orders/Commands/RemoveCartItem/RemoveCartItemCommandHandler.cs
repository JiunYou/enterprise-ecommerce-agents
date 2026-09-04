using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;

internal sealed class RemoveCartItemCommandHandler : ICommandHandler<RemoveCartItemCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public RemoveCartItemCommandHandler(
        IOrderRepository orderRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.ItemNotFound);
        }

        var productId = new ProductId(request.ProductId);
        var removeResult = order.RemoveItem(productId);

        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
