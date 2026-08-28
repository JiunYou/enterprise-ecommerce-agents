using EnterpriseCommerce.Application.Orders.Commands.ExpireOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseCommerce.Infrastructure.BackgroundJobs;

public class ExpiredOrdersCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredOrdersCleanupBackgroundService> _logger;
    private readonly TimeSpan _expirationWindow;
    private readonly TimeSpan _pollInterval;

    public ExpiredOrdersCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<ExpiredOrdersCleanupBackgroundService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        var expirationStr = configuration["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
        var expirationMinutes = int.TryParse(expirationStr, out var e) ? e : 15;
        _expirationWindow = TimeSpan.FromMinutes(expirationMinutes);
        
        var pollStr = configuration["BackgroundJobs:ExpiredOrdersCleanup:PollIntervalSeconds"];
        var pollSeconds = int.TryParse(pollStr, out var p) ? p : 60;
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired orders.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var threshold = DateTimeOffset.UtcNow.Subtract(_expirationWindow);

            var expiredOrderIds = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Submitted && o.SubmittedAt <= threshold)
                .Select(o => o.Id.Value)
                .Take(50) // Process in batches
                .ToListAsync(cancellationToken);

            foreach (var orderId in expiredOrderIds)
            {
                try
                {
                    _logger.LogInformation("Cancelling expired order: {OrderId}", orderId);
                    
                    var command = new ExpireOrderCommand(orderId);
                    var result = await sender.Send(command, cancellationToken);
                    
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Failed to cancel expired order {OrderId}. Error: {Error}", orderId, result.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while cancelling expired order {OrderId}.", orderId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expired orders");
        }
    }
}
