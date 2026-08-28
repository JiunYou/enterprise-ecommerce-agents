namespace EnterpriseCommerce.Infrastructure.Messaging;

public interface IRetryPolicy
{
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
