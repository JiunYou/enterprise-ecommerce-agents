namespace EnterpriseCommerce.Infrastructure.Messaging;

public interface IIdempotentEventChecker
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}
