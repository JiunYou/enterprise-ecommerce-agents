namespace EnterpriseCommerce.WebApi.Contracts.Catalog;

public record CreateProductRequest(
    string Name,
    string Sku,
    decimal Price,
    string Currency);
