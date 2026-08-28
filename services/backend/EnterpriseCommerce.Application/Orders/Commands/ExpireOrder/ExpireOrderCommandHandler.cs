using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.Application.Orders.Commands.ExpireOrder;

internal sealed class ExpireOrderCommandHandler : ICommandHandler<ExpireOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public ExpireOrderCommandHandler(IOrderRepository orderRepository, IApplicationUnitOfWork unitOfWork, IConfiguration configuration, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(ExpireOrderCommand request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        var expirationStr = _configuration["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
        var expirationMinutes = int.TryParse(expirationStr, out var e) ? e : 15;
        var threshold = _timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(expirationMinutes));

        // Only allow expiration for Submitted orders and if the threshold is passed
        if (!order.IsExpired(threshold))
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        var cancelResult = order.Cancel();

        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
