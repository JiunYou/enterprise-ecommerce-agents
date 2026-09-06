using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;

/// <summary>
/// 管理員取消訂單命令。
/// </summary>
public sealed record AdminCancelOrderCommand(
    Guid OrderId,
    string ActorIssuer,
    string ActorSubject,
    string Reason) : ICommand;
