using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;

public sealed record GetCustomerOrdersQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 25)
    : IQuery<CustomerOrderPageResponse>;
