using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Commands.RemoveWishlistItem;

public sealed record RemoveWishlistItemCommand(Guid CustomerId, Guid ProductId) : ICommand;
