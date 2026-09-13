namespace EnterpriseCommerce.WebApi.Contracts.Catalog;

public sealed record ProductAvailabilityResponse(
    Guid ProductId,
    int AvailableQuantity,
    bool InStock);
