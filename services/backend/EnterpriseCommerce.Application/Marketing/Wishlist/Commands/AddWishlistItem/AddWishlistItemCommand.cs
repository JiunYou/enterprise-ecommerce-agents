using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Commands.AddWishlistItem;

public sealed record AddWishlistItemCommand(Guid CustomerId, Guid ProductId) : ICommand;
