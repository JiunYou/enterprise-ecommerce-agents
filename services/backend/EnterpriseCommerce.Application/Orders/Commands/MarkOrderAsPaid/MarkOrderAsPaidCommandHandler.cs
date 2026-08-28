using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid;

internal sealed class MarkOrderAsPaidCommandHandler : ICommandHandler<MarkOrderAsPaidCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public MarkOrderAsPaidCommandHandler(IOrderRepository orderRepository, IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(MarkOrderAsPaidCommand request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        var payResult = order.MarkAsPaid();

        if (payResult.IsFailure)
        {
            return payResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
