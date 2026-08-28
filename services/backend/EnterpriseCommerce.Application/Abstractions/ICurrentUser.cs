namespace EnterpriseCommerce.Application.Abstractions;

public interface ICurrentUser
{
    Guid? Id { get; }
}
