using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentValidation;

namespace EnterpriseCommerce.Application.Inventory.Commands.DecreaseInventoryStock;

public sealed record DecreaseInventoryStockCommand(Guid ProductId, int Quantity) : ICommand;

public sealed class DecreaseInventoryStockCommandValidator : AbstractValidator<DecreaseInventoryStockCommand>
{
    public DecreaseInventoryStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

internal sealed class DecreaseInventoryStockCommandHandler : ICommandHandler<DecreaseInventoryStockCommand>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public DecreaseInventoryStockCommandHandler(
        IInventoryRepository inventoryRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DecreaseInventoryStockCommand request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return Result.Failure(InventoryErrors.NegativeQuantity);
        }

        var productReference = new ProductReference(request.ProductId);
        var inventoryItem = await _inventoryRepository.GetByProductIdAsync(productReference, cancellationToken);

        if (inventoryItem is null)
        {
            return Result.Failure(InventoryErrors.NotFound);
        }

        var decreaseResult = inventoryItem.DecreaseStock(new StockQuantity(request.Quantity));
        if (decreaseResult.IsFailure)
        {
            return Result.Failure(decreaseResult.Error);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.GetType().FullName?.Contains("DbUpdateConcurrencyException") == true)
        {
            return Result.Failure(InventoryErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}
