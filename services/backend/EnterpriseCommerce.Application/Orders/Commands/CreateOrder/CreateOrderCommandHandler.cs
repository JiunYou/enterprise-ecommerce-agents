using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.CreateOrder;

internal sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = Order.Create(request.CustomerId, request.Currency);
        
        _orderRepository.Add(order);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id.Value);
    }
}
