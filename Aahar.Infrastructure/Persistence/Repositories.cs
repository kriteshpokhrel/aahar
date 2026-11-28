using System.Data;
using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aahar.Infrastructure;

public sealed class EfUnitOfWork(RestaurantDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginSerializableAsync(CancellationToken ct) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct));
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

public sealed class EfRepositories(RestaurantDbContext db) : IUserRepository, IRestaurantRepository, IGuestRequestRepository, IInventoryRepository, IReportRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Username == username, ct);
    public Task<User?> FindForPasswordChangeAsync(string username, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username, ct);
    public void AddUser(User user) => db.Users.Add(user);
    public Task<int> ChangePasswordIfUnchangedAsync(int id, string oldHash, string newHash, CancellationToken ct) =>
        PasswordChange.UnchangedUser(db.Users, id, oldHash).ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, newHash), ct);

    public Task<List<MenuItem>> MenuAsync(bool availableOnly, CancellationToken ct) => db.MenuItems.AsNoTracking().Where(x => !availableOnly || x.IsAvailable).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<MenuItem?> FindMenuAsync(int id, CancellationToken ct) => await db.MenuItems.FindAsync([id], ct);
    public Task<bool> MenuExistsAsync(int id, bool availableOnly, CancellationToken ct) => db.MenuItems.AsNoTracking().AnyAsync(x => x.Id == id && (!availableOnly || x.IsAvailable), ct);
    public async Task<Dictionary<int, MenuItem>> MenuByIdsAsync(int[] ids, bool lockForShare, CancellationToken ct)
    {
        if (lockForShare)
        {
            ids = ids.Distinct().Order().ToArray();
            return (await db.MenuItems.FromSqlInterpolated($"SELECT * FROM \"MenuItems\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR SHARE").ToListAsync(ct)).ToDictionary(x => x.Id);
        }
        return await db.MenuItems.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }
    public void AddMenu(MenuItem item) => db.MenuItems.Add(item);
    public Task<List<TableDto>> TablesAsync(CancellationToken ct) => db.Tables.AsNoTracking().OrderBy(x => x.Number).Select(x => new TableDto(x.Id, x.Number, x.Capacity,
        db.Orders.Any(o => o.TableId == x.Id && (o.Status == "Draft" || o.Status == "Sent" || o.Status == "Completed")))).ToListAsync(ct);
    public Task<bool> TableExistsAsync(int id, CancellationToken ct) => db.Tables.AnyAsync(x => x.Id == id, ct);
    public Task<bool> TableOccupiedAsync(int id, CancellationToken ct) => db.Orders.AnyAsync(x => x.TableId == id && (x.Status == "Draft" || x.Status == "Sent" || x.Status == "Completed"), ct);
    public void AddTable(DiningTable table) => db.Tables.Add(table);
    public Task<List<Ingredient>> IngredientsAsync(bool lowOnly, CancellationToken ct) => db.Ingredients.AsNoTracking().Where(x => !lowOnly || x.Stock <= x.LowStockThreshold).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<Ingredient?> FindIngredientAsync(int id, CancellationToken ct) => await db.Ingredients.FindAsync([id], ct);
    public Task<bool> IngredientExistsAsync(int id, CancellationToken ct) => db.Ingredients.AnyAsync(x => x.Id == id, ct);
    public Task<int> CountIngredientsAsync(int[] ids, CancellationToken ct) => db.Ingredients.CountAsync(x => ids.Contains(x.Id), ct);
    public Task<Dictionary<int, Ingredient>> IngredientsByIdsAsync(int[] ids, CancellationToken ct) => db.Ingredients.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id, ct);
    public void AddIngredient(Ingredient ingredient) => db.Ingredients.Add(ingredient);
    public void AddThresholdChange(IngredientThresholdChange change) => db.IngredientThresholdChanges.Add(change);
    public void AddMovement(StockMovement movement) => db.StockMovements.Add(movement);
    public void AddMovements(IEnumerable<StockMovement> movements) => db.StockMovements.AddRange(movements);
    public Task<List<StockMovement>> MovementsAsync(int id, long? beforeId, int limit, CancellationToken ct) => db.StockMovements.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId)).OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<List<StockMovement>> ConsumptionAsync(int orderId, CancellationToken ct) => db.StockMovements.Include(x => x.Ingredient).Where(x => x.OrderId == orderId && x.Kind == "Consumption").ToListAsync(ct);
    public Task<List<RecipeLine>> RecipeLinesAsync(int menuId, CancellationToken ct) => db.Recipes.AsNoTracking().Where(x => x.MenuItemId == menuId).OrderBy(x => x.IngredientId).Select(x => new RecipeLine(x.IngredientId, x.Quantity)).ToListAsync(ct);
    public Task<MenuItem?> MenuWithRecipeAsync(int menuId, CancellationToken ct) => db.MenuItems.Include(x => x.Recipe).SingleOrDefaultAsync(x => x.Id == menuId, ct);
    public Task<List<RecipeIngredient>> RecipesForMenuAsync(int[] ids, CancellationToken ct) => db.Recipes.Include(x => x.Ingredient).Where(x => ids.Contains(x.MenuItemId)).ToListAsync(ct);
    public void RemoveRecipes(IEnumerable<RecipeIngredient> recipes) => db.Recipes.RemoveRange(recipes);
    public void AddRecipes(IEnumerable<RecipeIngredient> recipes) => db.Recipes.AddRange(recipes);
    public Task<List<OrderDto>> OrdersAsync(CancellationToken ct) => OrderQueries.Orders(db.Orders.AsNoTracking().AsSingleQuery()).ToListAsync(ct);
    public Task<Order?> FindOrderAsync(int id, CancellationToken ct) => db.Orders.AsSplitQuery().Include(x => x.Items).Include(x => x.Payment).Include(x => x.Tickets).ThenInclude(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddOrder(Order order) => db.Orders.Add(order);
    public void RemoveOrderItems(IEnumerable<OrderItem> items) => db.Set<OrderItem>().RemoveRange(items);
    public async Task<SalesDto> SalesAsync(CancellationToken ct) => new(await db.Payments.CountAsync(ct), await db.Payments.SumAsync(x => x.Amount, ct));
    public Task<List<KitchenTicket>> TicketsAsync(CancellationToken ct) => db.KitchenTickets.AsNoTracking().Include(x => x.Order).Include(x => x.Items).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public Task<KitchenTicket?> FindTicketAsync(int id, CancellationToken ct) => db.KitchenTickets.Include(x => x.Order).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);

    public void Add(GuestRequest request) => db.GuestRequests.Add(request);
    public Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct) => db.GuestRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct) => db.GuestRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct) => db.GuestRequests.AsNoTracking().Include(x => x.Items).Where(x => (beforeId == null || x.Id < beforeId) && (status == null || x.Status == status)).OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);

    public Task<List<Supplier>> SuppliersAsync(CancellationToken ct) => db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<Supplier?> FindSupplierAsync(int id, bool lockForShare, CancellationToken ct) => lockForShare
        ? (await db.Suppliers.FromSqlInterpolated($"SELECT * FROM \"Suppliers\" WHERE \"Id\" = {id} FOR SHARE").ToListAsync(ct)).SingleOrDefault()
        : await db.Suppliers.FindAsync([id], ct);
    public Task<bool> SupplierExistsAsync(int id, CancellationToken ct) => db.Suppliers.AnyAsync(x => x.Id == id, ct);
    public void AddSupplier(Supplier supplier) => db.Suppliers.Add(supplier);
    public void AddSupplierChange(SupplierChange change) => db.SupplierChanges.Add(change);
    public Task<List<SupplierChange>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) => db.SupplierChanges.AsNoTracking().Where(x => x.SupplierId == id && (beforeId == null || x.Id < beforeId)).OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<List<IngredientThresholdChange>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) => db.IngredientThresholdChanges.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId)).OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public void AddReceipt(PurchaseReceipt receipt) => db.PurchaseReceipts.Add(receipt);
    public Task<List<PurchaseReceipt>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct) => db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).Where(x => beforeId == null || x.Id < beforeId).OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<PurchaseReceipt?> FindReceiptAsync(int id, CancellationToken ct) => db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PaidSalesTotals?> PaidSalesAsync(BusinessDateRange range, CancellationToken ct) => ReportQueries.PaidSales(db.Payments.AsNoTracking(), range).SingleOrDefaultAsync(ct);
    public Task<List<BestSellingItemDto>> BestSellingAsync(BusinessDateRange range, int limit, CancellationToken ct) => ReportQueries.BestSelling(db.Set<OrderItem>().AsNoTracking(), range, limit).ToListAsync(ct);
}
