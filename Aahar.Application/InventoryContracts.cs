namespace Aahar.Application;

public record SupplierRequest(string Name, string? Contact, string Reason);
public record SupplierDto(int Id, string Name, string? Contact);
public record SupplierChangeDto(long Id, int SupplierId, string Name, string? Contact, string Actor, string Reason, DateTime OccurredAt);
public record ThresholdRequest(decimal? Threshold, string Reason);
public record ThresholdChangeDto(long Id, int IngredientId, decimal? PreviousThreshold, decimal NewThreshold, string Actor, string Reason, DateTime OccurredAt);
public record WasteRequest(decimal Quantity, string Unit, string Reason);
public record PurchaseLineRequest(int IngredientId, decimal Quantity, string Unit, decimal? UnitCost);
public record PurchaseReceiptRequest(int SupplierId, string Reference, string Reason, List<PurchaseLineRequest> Lines);
public record PurchaseLineDto(int Id, int IngredientId, string IngredientName, string Unit, decimal Quantity, decimal UnitCost, decimal LineTotal);
public record PurchaseReceiptDto(int Id, int SupplierId, string SupplierName, string Reference, string Reason, string Actor, DateTime ReceivedAt, decimal Total, List<PurchaseLineDto> Lines);

public interface IInventoryService
{
    Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct);
    Task<SupplierDto> SaveSupplierAsync(int? id, SupplierRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<SupplierChangeDto>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<IReadOnlyList<IngredientDto>> LowStockAsync(CancellationToken ct);
    Task<IngredientDto> ChangeThresholdAsync(int id, ThresholdRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<ThresholdChangeDto>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<IngredientDto> WasteAsync(int id, WasteRequest request, string actor, CancellationToken ct);
    Task<PurchaseReceiptDto> ReceiveAsync(PurchaseReceiptRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<PurchaseReceiptDto>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct);
    Task<PurchaseReceiptDto> ReceiptAsync(int id, CancellationToken ct);
}
