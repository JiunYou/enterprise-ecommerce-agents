using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Commands.DeactivateCoupon;

public sealed record DeactivateCouponCommand(Guid Id) : ICommand;
