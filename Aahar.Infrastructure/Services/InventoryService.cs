using Aahar.Application;
using Aahar.Domain;
using Mapster;

namespace Aahar.Infrastructure;

public sealed class InventoryService(IInventoryRepository repository, IUnitOfWork unitOfWork) : IInventoryService
{
    private static PurchaseReceiptDto ToDto(PurchaseReceipt receipt) => new(receipt.Id, receipt.SupplierId, receipt.SupplierName,
        receipt.Reference, receipt.Reason, receipt.Actor, receipt.ReceivedAt, receipt.Total, receipt.Lines.Adapt<List<PurchaseLineDto>>());

    private static void CheckPage(long? beforeId, int limit)
    {
        if (beforeId is <= 0 || limit is < 1 or > 100) throw new DomainException("Invalid page.");
    }

    public async Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct) =>
        (await repository.SuppliersAsync(ct)).Adapt<List<SupplierDto>>();

    public async Task<SupplierDto> SaveSupplierAsync(int? id, SupplierRequest r, string actor, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var supplier = id is null ? new Supplier() : await repository.FindSupplierAsync(id.Value, ct) ?? throw new DomainException("Supplier not found.");
        var change = InventoryRules.ChangeSupplier(supplier, r.Name, r.Contact, r.Reason, actor);
        if (id is null) repository.AddSupplier(supplier);
        repository.AddSupplierChange(change);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return supplier.Adapt<SupplierDto>();
    }

    public async Task<IReadOnlyList<SupplierChangeDto>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        if (!await repository.SupplierExistsAsync(id, ct)) throw new DomainException("Supplier not found.");
        return (await repository.SupplierHistoryAsync(id, beforeId, limit, ct)).Adapt<List<SupplierChangeDto>>();
    }

    public async Task<IReadOnlyList<IngredientDto>> LowStockAsync(CancellationToken ct) =>
        (await repository.IngredientsAsync(true, ct))
            .Select(RestaurantService.ToDto).ToList();

    public async Task<IngredientDto> ChangeThresholdAsync(int id, ThresholdRequest r, string actor, CancellationToken ct)
    {
        if (r.Threshold is null) throw new DomainException("Threshold is required.");
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var ingredient = await repository.FindIngredientAsync(id, ct) ?? throw new DomainException("Ingredient not found.");
        repository.AddThresholdChange(InventoryRules.ChangeThreshold(ingredient, r.Threshold.Value, r.Reason, actor));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RestaurantService.ToDto(ingredient);
    }

    public async Task<IReadOnlyList<ThresholdChangeDto>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        if (!await repository.IngredientExistsAsync(id, ct)) throw new DomainException("Ingredient not found.");
        return (await repository.ThresholdHistoryAsync(id, beforeId, limit, ct)).Adapt<List<ThresholdChangeDto>>();
    }

    public async Task<IngredientDto> WasteAsync(int id, WasteRequest r, string actor, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var ingredient = await repository.FindIngredientAsync(id, ct) ?? throw new DomainException("Ingredient not found.");
        repository.AddMovement(InventoryRules.Waste(ingredient, r.Quantity, r.Unit, r.Reason, actor));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RestaurantService.ToDto(ingredient);
    }

    public async Task<PurchaseReceiptDto> ReceiveAsync(PurchaseReceiptRequest r, string actor, CancellationToken ct)
    {
        if (r.SupplierId <= 0 || r.Lines is not { Count: > 0 and <= 100 } || r.Lines.Any(x => x is null))
            throw new DomainException("Invalid purchase receipt.");
        var lines = r.Lines.Select(x => new PurchaseLine(x.IngredientId, x.Quantity, x.Unit, x.UnitCost)).ToList();
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var supplier = await repository.LockSupplierForShareAsync(r.SupplierId, ct)
            ?? throw new DomainException("Supplier not found.");
        var ids = lines.Select(x => x.IngredientId).Distinct().ToArray();
        var ingredients = await repository.IngredientsByIdsAsync(ids, ct);
        var (receipt, movements) = InventoryRules.Receive(supplier, r.Reference, r.Reason, lines, ingredients, actor);
        repository.AddReceipt(receipt);
        repository.AddMovements(movements);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(receipt);
    }

    public async Task<IReadOnlyList<PurchaseReceiptDto>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        return (await repository.ReceiptsAsync(beforeId, limit, ct)).Select(ToDto).ToList();
    }

    public async Task<PurchaseReceiptDto> ReceiptAsync(int id, CancellationToken ct) =>
        ToDto(await repository.FindReceiptAsync(id, ct)
            ?? throw new DomainException("Purchase receipt not found."));
}
