using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using System;

namespace EnterpriseCommerce.Application.Orders.Commands.ApplyCouponToCart;

public sealed record ApplyCouponToCartCommand(Guid CustomerId, string Code) : ICommand<CartResponse>;
