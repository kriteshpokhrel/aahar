using Aahar.Domain;

namespace Aahar.Application;

public interface IInventoryRepository
{
    Task<List<Ingredient>> IngredientsAsync(bool lowOnly, CancellationToken ct);
    Task<Ingredient?> FindIngredientAsync(int id, CancellationToken ct);
    Task<bool> IngredientExistsAsync(int id, CancellationToken ct);
    Task<int> CountIngredientsAsync(int[] ids, CancellationToken ct);
    Task<Dictionary<int, Ingredient>> IngredientsByIdsAsync(int[] ids, CancellationToken ct);
    void AddIngredient(Ingredient ingredient);
    void AddThresholdChange(IngredientThresholdChange change);
    void AddMovement(StockMovement movement);
    void AddMovements(IEnumerable<StockMovement> movements);
    Task<List<StockMovement>> MovementsAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<List<StockMovement>> ConsumptionAsync(int orderId, CancellationToken ct);
    Task<List<IngredientThresholdChange>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<List<Supplier>> SuppliersAsync(CancellationToken ct);
    Task<Supplier?> FindSupplierAsync(int id, CancellationToken ct);
    Task<Supplier?> LockSupplierForShareAsync(int id, CancellationToken ct);
    Task<bool> SupplierExistsAsync(int id, CancellationToken ct);
    void AddSupplier(Supplier supplier);
    void AddSupplierChange(SupplierChange change);
    Task<List<SupplierChange>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    void AddReceipt(PurchaseReceipt receipt);
    Task<List<PurchaseReceipt>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct);
    Task<PurchaseReceipt?> FindReceiptAsync(int id, CancellationToken ct);
}
