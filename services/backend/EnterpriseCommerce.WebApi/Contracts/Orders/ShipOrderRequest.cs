namespace EnterpriseCommerce.WebApi.Contracts.Orders;

public sealed record ShipOrderRequest(
    string Carrier,
    string TrackingNumber);
