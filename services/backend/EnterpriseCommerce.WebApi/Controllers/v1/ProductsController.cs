using Asp.Versioning;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Application.Catalog.Commands.DeactivateProduct;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductPrice;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;
using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
using EnterpriseCommerce.Application.Common.Models;
using EnterpriseCommerce.WebApi.Contracts.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
public class ProductsController : ApiControllerBase
{
    public ProductsController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedList<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? onlyActive = true,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        // 安全邊界：非 Admin 使用者強制僅能查閱已上架 (IsActive == true) 的商品
        bool? effectiveOnlyActive = User.IsInRole("Admin") ? onlyActive : true;

        var query = new GetProductsQuery(page, pageSize, effectiveOnlyActive, searchTerm, sortBy, sortOrder);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}", Name = nameof(GetProductById))]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        bool isAdmin = User.IsInRole("Admin");
        var query = new GetProductByIdQuery(id, AllowInactive: isAdmin);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("sku/{sku}", Name = nameof(GetProductBySku))]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProductBySku(string sku, CancellationToken cancellationToken)
    {
        bool isAdmin = User.IsInRole("Admin");
        var query = new GetProductBySkuQuery(sku, AllowInactive: isAdmin);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(request.Name, request.Sku, request.Price, request.Currency);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return CreatedAtAction(nameof(GetProductById), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}/price")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProductPrice(Guid id, [FromBody] UpdateProductPriceRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductPriceCommand(id, request.NewPrice);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateProduct(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateProductCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }
}
