using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using System;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveCouponFromCart;

public sealed record RemoveCouponFromCartCommand(Guid CustomerId) : ICommand<CartResponse>;
