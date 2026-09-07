namespace EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;

/// <summary>
/// Admin 退款操作的中立結果狀態。
/// </summary>
public enum AdminRefundOutcome
{
    RefundCompleted,
    AlreadyRefunded,
    ManualProviderResolutionRequired,
    Unresolved,
    ExecutionTemporarilyBlocked
}
