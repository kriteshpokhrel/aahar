using System.Data;
using Aahar.Application;
using Aahar.Domain;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class InventoryService(RestaurantDbContext db) : IInventoryService
{
    private static PurchaseReceiptDto ToDto(PurchaseReceipt receipt) => new(receipt.Id, receipt.SupplierId, receipt.SupplierName,
        receipt.Reference, receipt.Reason, receipt.Actor, receipt.ReceivedAt, receipt.Total, receipt.Lines.Adapt<List<PurchaseLineDto>>());

    private static void CheckPage(long? beforeId, int limit)
    {
        if (beforeId is <= 0 || limit is < 1 or > 100) throw new DomainException("Invalid page.");
    }

    public async Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct) =>
        (await db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Adapt<List<SupplierDto>>();

    public async Task<SupplierDto> SaveSupplierAsync(int? id, SupplierRequest r, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var supplier = id is null ? new Supplier() : await db.Suppliers.FindAsync([id.Value], ct) ?? throw new DomainException("Supplier not found.");
        var change = InventoryRules.ChangeSupplier(supplier, r.Name, r.Contact, r.Reason, actor);
        if (id is null) db.Suppliers.Add(supplier);
        db.SupplierChanges.Add(change);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return supplier.Adapt<SupplierDto>();
    }

    public async Task<IReadOnlyList<SupplierChangeDto>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        if (!await db.Suppliers.AnyAsync(x => x.Id == id, ct)) throw new DomainException("Supplier not found.");
        return (await db.SupplierChanges.AsNoTracking().Where(x => x.SupplierId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct)).Adapt<List<SupplierChangeDto>>();
    }

    public async Task<IReadOnlyList<IngredientDto>> LowStockAsync(CancellationToken ct) =>
        (await db.Ingredients.AsNoTracking().Where(x => x.Stock <= x.LowStockThreshold).OrderBy(x => x.Name).ToListAsync(ct))
            .Select(RestaurantService.ToDto).ToList();

    public async Task<IngredientDto> ChangeThresholdAsync(int id, ThresholdRequest r, string actor, CancellationToken ct)
    {
        if (r.Threshold is null) throw new DomainException("Threshold is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ingredient = await db.Ingredients.FindAsync([id], ct) ?? throw new DomainException("Ingredient not found.");
        db.IngredientThresholdChanges.Add(InventoryRules.ChangeThreshold(ingredient, r.Threshold.Value, r.Reason, actor));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RestaurantService.ToDto(ingredient);
    }

    public async Task<IReadOnlyList<ThresholdChangeDto>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        if (!await db.Ingredients.AnyAsync(x => x.Id == id, ct)) throw new DomainException("Ingredient not found.");
        return (await db.IngredientThresholdChanges.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct)).Adapt<List<ThresholdChangeDto>>();
    }

    public async Task<IngredientDto> WasteAsync(int id, WasteRequest r, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ingredient = await db.Ingredients.FindAsync([id], ct) ?? throw new DomainException("Ingredient not found.");
        db.StockMovements.Add(InventoryRules.Waste(ingredient, r.Quantity, r.Unit, r.Reason, actor));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RestaurantService.ToDto(ingredient);
    }

    public async Task<PurchaseReceiptDto> ReceiveAsync(PurchaseReceiptRequest r, string actor, CancellationToken ct)
    {
        if (r.SupplierId <= 0 || r.Lines is not { Count: > 0 and <= 100 } || r.Lines.Any(x => x is null))
            throw new DomainException("Invalid purchase receipt.");
        var lines = r.Lines.Select(x => new PurchaseLine(x.IngredientId, x.Quantity, x.Unit, x.UnitCost)).ToList();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var supplier = (await db.Suppliers.FromSqlInterpolated(
            $"SELECT * FROM \"Suppliers\" WHERE \"Id\" = {r.SupplierId} FOR SHARE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new DomainException("Supplier not found.");
        var ids = lines.Select(x => x.IngredientId).Distinct().ToArray();
        var ingredients = await db.Ingredients.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id, ct);
        var (receipt, movements) = InventoryRules.Receive(supplier, r.Reference, r.Reason, lines, ingredients, actor);
        db.PurchaseReceipts.Add(receipt);
        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(receipt);
    }

    public async Task<IReadOnlyList<PurchaseReceiptDto>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct)
    {
        CheckPage(beforeId, limit);
        return (await db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).Where(x => beforeId == null || x.Id < beforeId)
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<PurchaseReceiptDto> ReceiptAsync(int id, CancellationToken ct) =>
        ToDto(await db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Purchase receipt not found."));
}
