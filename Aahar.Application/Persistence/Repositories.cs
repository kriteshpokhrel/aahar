using Aahar.Domain;

namespace Aahar.Application;

public interface IUnitOfWork
{
    Task<ITransaction> BeginSerializableAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<User?> FindForPasswordChangeAsync(string username, CancellationToken ct);
    void AddUser(User user);
    Task<int> ChangePasswordIfUnchangedAsync(int id, string oldHash, string newHash, CancellationToken ct);
}

public interface IRestaurantRepository
{
    Task<List<MenuItem>> MenuAsync(bool availableOnly, CancellationToken ct);
    Task<MenuItem?> FindMenuAsync(int id, CancellationToken ct);
    Task<bool> MenuExistsAsync(int id, bool availableOnly, CancellationToken ct);
    Task<Dictionary<int, MenuItem>> MenuByIdsAsync(int[] ids, bool lockForShare, CancellationToken ct);
    void AddMenu(MenuItem item);
    Task<List<TableDto>> TablesAsync(CancellationToken ct);
    Task<bool> TableExistsAsync(int id, CancellationToken ct);
    Task<bool> TableOccupiedAsync(int id, CancellationToken ct);
    void AddTable(DiningTable table);
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
    Task<List<RecipeLine>> RecipeLinesAsync(int menuId, CancellationToken ct);
    Task<MenuItem?> MenuWithRecipeAsync(int menuId, CancellationToken ct);
    Task<List<RecipeIngredient>> RecipesForMenuAsync(int[] ids, CancellationToken ct);
    void RemoveRecipes(IEnumerable<RecipeIngredient> recipes);
    void AddRecipes(IEnumerable<RecipeIngredient> recipes);
    Task<List<OrderDto>> OrdersAsync(CancellationToken ct);
    Task<Order?> FindOrderAsync(int id, CancellationToken ct);
    void AddOrder(Order order);
    void RemoveOrderItems(IEnumerable<OrderItem> items);
    Task<SalesDto> SalesAsync(CancellationToken ct);
    Task<List<KitchenTicket>> TicketsAsync(CancellationToken ct);
    Task<KitchenTicket?> FindTicketAsync(int id, CancellationToken ct);
}

public interface IGuestRequestRepository
{
    void Add(GuestRequest request);
    Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct);
    Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct);
    Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct);
}

public interface IInventoryRepository
{
    Task<List<Supplier>> SuppliersAsync(CancellationToken ct);
    Task<Supplier?> FindSupplierAsync(int id, bool lockForShare, CancellationToken ct);
    Task<bool> SupplierExistsAsync(int id, CancellationToken ct);
    void AddSupplier(Supplier supplier);
    void AddSupplierChange(SupplierChange change);
    Task<List<SupplierChange>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<List<IngredientThresholdChange>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct);
    void AddReceipt(PurchaseReceipt receipt);
    Task<List<PurchaseReceipt>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct);
    Task<PurchaseReceipt?> FindReceiptAsync(int id, CancellationToken ct);
}

public record PaidSalesTotals(long OrdersPaid, decimal Total);
public interface IReportRepository
{
    Task<PaidSalesTotals?> PaidSalesAsync(BusinessDateRange range, CancellationToken ct);
    Task<List<BestSellingItemDto>> BestSellingAsync(BusinessDateRange range, int limit, CancellationToken ct);
}

public interface IUserPasswords
{
    bool Verify(User user, string password);
    string Hash(User user, string password);
}
