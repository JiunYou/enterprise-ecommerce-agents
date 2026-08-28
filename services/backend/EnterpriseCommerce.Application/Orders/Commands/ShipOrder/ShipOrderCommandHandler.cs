using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.ShipOrder;

internal sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public ShipOrderCommandHandler(IOrderRepository orderRepository, IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        var shipResult = order.Ship();

        if (shipResult.IsFailure)
        {
            return shipResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
