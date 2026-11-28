using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class InventoryRepository(RestaurantDbContext db) : IInventoryRepository
{
    public Task<List<Ingredient>> IngredientsAsync(bool lowOnly, CancellationToken ct) =>
        db.Ingredients.AsNoTracking().Where(x => !lowOnly || x.Stock <= x.LowStockThreshold).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<Ingredient?> FindIngredientAsync(int id, CancellationToken ct) => await db.Ingredients.FindAsync([id], ct);
    public Task<bool> IngredientExistsAsync(int id, CancellationToken ct) => db.Ingredients.AnyAsync(x => x.Id == id, ct);
    public Task<int> CountIngredientsAsync(int[] ids, CancellationToken ct) => db.Ingredients.CountAsync(x => ids.Contains(x.Id), ct);
    public Task<Dictionary<int, Ingredient>> IngredientsByIdsAsync(int[] ids, CancellationToken ct) =>
        db.Ingredients.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id, ct);
    public void AddIngredient(Ingredient ingredient) => db.Ingredients.Add(ingredient);
    public void AddThresholdChange(IngredientThresholdChange change) => db.IngredientThresholdChanges.Add(change);
    public void AddMovement(StockMovement movement) => db.StockMovements.Add(movement);
    public void AddMovements(IEnumerable<StockMovement> movements) => db.StockMovements.AddRange(movements);
    public Task<List<StockMovement>> MovementsAsync(int id, long? beforeId, int limit, CancellationToken ct) =>
        db.StockMovements.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<List<StockMovement>> ConsumptionAsync(int orderId, CancellationToken ct) =>
        db.StockMovements.Include(x => x.Ingredient).Where(x => x.OrderId == orderId && x.Kind == "Consumption").ToListAsync(ct);
    public Task<List<IngredientThresholdChange>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) =>
        db.IngredientThresholdChanges.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<List<Supplier>> SuppliersAsync(CancellationToken ct) => db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<Supplier?> FindSupplierAsync(int id, CancellationToken ct) => await db.Suppliers.FindAsync([id], ct);
    public async Task<Supplier?> LockSupplierForShareAsync(int id, CancellationToken ct) =>
        (await db.Suppliers.FromSqlInterpolated($"SELECT * FROM \"Suppliers\" WHERE \"Id\" = {id} FOR SHARE").ToListAsync(ct)).SingleOrDefault();
    public Task<bool> SupplierExistsAsync(int id, CancellationToken ct) => db.Suppliers.AnyAsync(x => x.Id == id, ct);
    public void AddSupplier(Supplier supplier) => db.Suppliers.Add(supplier);
    public void AddSupplierChange(SupplierChange change) => db.SupplierChanges.Add(change);
    public Task<List<SupplierChange>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) =>
        db.SupplierChanges.AsNoTracking().Where(x => x.SupplierId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public void AddReceipt(PurchaseReceipt receipt) => db.PurchaseReceipts.Add(receipt);
    public Task<List<PurchaseReceipt>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct) =>
        db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).Where(x => beforeId == null || x.Id < beforeId)
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<PurchaseReceipt?> FindReceiptAsync(int id, CancellationToken ct) =>
        db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
}
