namespace EnterpriseCommerce.Domain.Primitives;

public interface IBusinessRule
{
    string Message { get; }
    bool IsBroken();
}
