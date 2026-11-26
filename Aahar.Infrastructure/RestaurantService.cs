using System.Data;
using Aahar.Application;
using Aahar.Domain;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class RestaurantService(RestaurantDbContext db, IPasswordHasher<User> hasher) : IRestaurantService
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new DomainException(message);
    }
    private static bool ValidText(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool Money(decimal n) => n >= 0 && n <= 9999999999.99m && decimal.Round(n, 2) == n;
    private static bool Quantity(decimal n) => n >= 0 && n <= 99999999999.999m && decimal.Round(n, 3) == n;
    private static OrderDto ToDto(Order order) => new(order.Id, order.TableId, order.Status, order.CreatedAt,
        order.Items.Adapt<List<OrderItemDto>>(), order.Items.Sum(x => x.UnitPrice * x.Quantity), order.Payment?.Adapt<PaymentDto>(),
        order.CancelledAt, order.CancelledBy, order.CancellationReason, order.CancellationStockDisposition);
    private static KitchenTicketDto ToTicketDto(KitchenTicket ticket) => new(ticket.Id, ticket.OrderId, ticket.Order.TableId,
        ticket.Status, ticket.CreatedAt, ticket.IsReconstructed, ticket.Items.Adapt<List<KitchenTicketItemDto>>());
    private static List<DraftLine> DraftLines(List<OrderLineRequest>? items)
    {
        Check(items is { Count: > 0 and <= 100 } && items.All(x => x is not null), "Invalid order items.");
        return items!.Select(x => new DraftLine(x.MenuItemId, x.Quantity, x.Notes)).ToList();
    }
    private async Task<Dictionary<int, MenuItem>> MenuForLines(List<DraftLine> lines, CancellationToken ct)
    {
        var ids = lines.Select(x => x.MenuItemId).ToArray();
        return await db.MenuItems.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }

    public async Task<UserDto?> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        if (!ValidText(request.Username, 100) || request.Password is not { Length: > 0 and <= 256 }) return null;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == request.Username.Trim().ToLowerInvariant(), ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;
        return user.Adapt<UserDto>();
    }
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        Check(ValidText(request.Username, 100) && request.Password is { Length: >= 12 and <= 256 } &&
            request.Role is "Admin" or "Manager" or "Staff" or "Kitchen", "Invalid username, password, or role.");
        var user = new User { Username = request.Username.Trim().ToLowerInvariant(), Role = request.Role };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user); await db.SaveChangesAsync(ct); return user.Adapt<UserDto>();
    }
    public async Task<IReadOnlyList<MenuDto>> MenuAsync(bool availableOnly, CancellationToken ct) =>
        (await db.MenuItems.AsNoTracking().Where(x => !availableOnly || x.IsAvailable).OrderBy(x => x.Name).ToListAsync(ct)).Adapt<List<MenuDto>>();
    public async Task<MenuDto> SaveMenuAsync(int? id, MenuRequest r, CancellationToken ct)
    {
        Check(ValidText(r.Name, 100) && ValidText(r.Category, 100) && (r.Description?.Length ?? 0) <= 300 && Money(r.Price), "Invalid menu item.");
        var item = id is null ? new MenuItem() : await db.MenuItems.FindAsync([id.Value], ct) ?? throw new DomainException("Menu item not found.");
        item.Name = r.Name.Trim(); item.Category = r.Category.Trim(); item.Description = r.Description; item.Price = r.Price; item.IsAvailable = r.IsAvailable;
        if (id is null) db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Adapt<MenuDto>();
    }
    public async Task<IReadOnlyList<TableDto>> TablesAsync(CancellationToken ct) =>
        await db.Tables.AsNoTracking().OrderBy(x => x.Number).Select(x => new TableDto(x.Id, x.Number, x.Capacity,
            db.Orders.Any(o => o.TableId == x.Id && (o.Status == "Draft" || o.Status == "Sent" || o.Status == "Completed")))).ToListAsync(ct);
    public async Task<TableDto> CreateTableAsync(TableRequest r, CancellationToken ct)
    {
        Check(r.Number > 0 && r.Capacity is > 0 and <= 100, "Invalid table.");
        var table = new DiningTable { Number = r.Number, Capacity = r.Capacity };
        db.Tables.Add(table); await db.SaveChangesAsync(ct); return new TableDto(table.Id, table.Number, table.Capacity, false);
    }
    public async Task<IReadOnlyList<IngredientDto>> IngredientsAsync(CancellationToken ct) =>
        (await db.Ingredients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Adapt<List<IngredientDto>>();
    public async Task<IngredientDto> CreateIngredientAsync(IngredientRequest r, string actor, CancellationToken ct)
    {
        Check(ValidText(r.Name, 100) && ValidText(r.Unit, 30) && Quantity(r.Stock), "Invalid ingredient.");
        var ingredient = new Ingredient { Name = r.Name.Trim(), Unit = r.Unit.Trim(), Stock = r.Stock };
        db.Ingredients.Add(ingredient);
        db.StockMovements.Add(new StockMovement { Ingredient = ingredient, Kind = "Opening", QuantityChange = r.Stock, BalanceAfter = r.Stock, Actor = actor });
        await db.SaveChangesAsync(ct); return ingredient.Adapt<IngredientDto>();
    }
    public async Task<IngredientDto> AddStockAsync(int id, StockRequest r, string actor, CancellationToken ct)
    {
        Check(Quantity(r.Quantity) && r.Quantity > 0, "Stock quantity must be positive with at most three decimal places.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ingredient = await db.Ingredients.FindAsync([id], ct) ?? throw new DomainException("Ingredient not found.");
        Check(Quantity(ingredient.Stock + r.Quantity), "Stock exceeds the maximum.");
        ingredient.Stock += r.Quantity;
        db.StockMovements.Add(new StockMovement { IngredientId = id, Kind = "Receipt", QuantityChange = r.Quantity, BalanceAfter = ingredient.Stock, Actor = actor });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ingredient.Adapt<IngredientDto>();
    }
    public async Task<IReadOnlyList<StockMovementDto>> StockMovementsAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        Check(limit is > 0 and <= 100 && (beforeId is null || beforeId > 0), "Invalid movement page.");
        Check(await db.Ingredients.AnyAsync(x => x.Id == id, ct), "Ingredient not found.");
        return (await db.StockMovements.AsNoTracking().Where(x => x.IngredientId == id && (beforeId == null || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct)).Adapt<List<StockMovementDto>>();
    }
    public async Task<RecipeRequest> RecipeAsync(int menuId, CancellationToken ct)
    {
        Check(await db.MenuItems.AnyAsync(x => x.Id == menuId, ct), "Menu item not found.");
        var ingredients = await db.Recipes.AsNoTracking().Where(x => x.MenuItemId == menuId)
            .OrderBy(x => x.IngredientId).Select(x => new RecipeLine(x.IngredientId, x.Quantity)).ToListAsync(ct);
        return new RecipeRequest(ingredients);
    }
    public async Task SetRecipeAsync(int menuId, RecipeRequest r, CancellationToken ct)
    {
        Check(r.Ingredients is not null && r.Ingredients.Count <= 100 && r.Ingredients.All(x => x is not null && x.IngredientId > 0 && Quantity(x.Quantity) && x.Quantity > 0) &&
            r.Ingredients.Select(x => x.IngredientId).Distinct().Count() == r.Ingredients.Count, "Invalid recipe.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var item = await db.MenuItems.Include(x => x.Recipe).SingleOrDefaultAsync(x => x.Id == menuId, ct) ?? throw new DomainException("Menu item not found.");
        var ids = r.Ingredients!.Select(x => x.IngredientId).ToArray();
        Check(await db.Ingredients.CountAsync(x => ids.Contains(x.Id), ct) == ids.Length, "Ingredient not found.");
        db.Recipes.RemoveRange(item.Recipe);
        await db.SaveChangesAsync(ct);
        db.Recipes.AddRange(r.Ingredients!.Select(x => new RecipeIngredient { MenuItemId = menuId, IngredientId = x.IngredientId, Quantity = x.Quantity }));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct) =>
        (await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Payment).OrderByDescending(x => x.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();
    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest r, CancellationToken ct)
    {
        Check(r.TableId is null or > 0, "Invalid table.");
        var lines = DraftLines(r.Items);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (r.TableId is not null) Check(await db.Tables.AnyAsync(x => x.Id == r.TableId, ct), "Table not found.");
        var order = new Order { TableId = r.TableId };
        OrderRules.SetDraftItems(order, lines, await MenuForLines(lines, ct));
        db.Orders.Add(order); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    private async Task<Order> GetOrder(int id, CancellationToken ct) =>
        await db.Orders.Include(x => x.Items).Include(x => x.Payment).Include(x => x.Ticket).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Order not found.");
    public async Task<OrderDto> EditOrderItemsAsync(int id, EditOrderItemsRequest r, CancellationToken ct)
    {
        var lines = DraftLines(r.Items);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await GetOrder(id, ct);
        var oldItems = order.Items.ToArray();
        OrderRules.SetDraftItems(order, lines, await MenuForLines(lines, ct));
        db.Set<OrderItem>().RemoveRange(oldItems.Except(order.Items));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> SendOrderAsync(int id, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await GetOrder(id, ct);
        OrderRules.RequireTransition(order.Status, "Draft");
        var ids = order.Items.Select(x => x.MenuItemId).ToArray();
        var recipes = await db.Recipes.Include(x => x.Ingredient).Where(x => ids.Contains(x.MenuItemId)).ToListAsync(ct);
        db.StockMovements.AddRange(OrderRules.DeductStock(order, recipes, actor));
        order.Ticket = KitchenRules.CreateTicket(order);
        OrderRules.Touch(order);
        order.Status = "Sent";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest r, string actor, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await GetOrder(id, ct);
        var consumed = await db.StockMovements.Include(x => x.Ingredient)
            .Where(x => x.OrderId == id && x.Kind == "Consumption").ToListAsync(ct);
        db.StockMovements.AddRange(OrderRules.Cancel(order, consumed, r.Reason, r.StockDisposition, actor, role));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await GetOrder(id, ct); KitchenRules.Complete(order);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> PayAsync(int id, PaymentRequest r, CancellationToken ct)
    {
        Check(r.Method is "Cash" or "Card", "Payment method must be Cash or Card.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await GetOrder(id, ct); OrderRules.RequireTransition(order.Status, "Completed");
        Check(order.Payment is null, "Order is already paid.");
        OrderRules.Touch(order);
        order.Payment = new Payment { Method = r.Method, Amount = order.Items.Sum(x => x.UnitPrice * x.Quantity) };
        order.Status = "Paid"; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<SalesDto> SalesAsync(CancellationToken ct) =>
        new(await db.Payments.CountAsync(ct), await db.Payments.SumAsync(x => x.Amount, ct));
    public async Task<IReadOnlyList<KitchenTicketDto>> KitchenTicketsAsync(CancellationToken ct) =>
        (await db.KitchenTickets.AsNoTracking().Include(x => x.Order).Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct)).Select(ToTicketDto).ToList();
    private async Task<KitchenTicketDto> TransitionTicketAsync(int id, string from, string to, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ticket = await db.KitchenTickets.Include(x => x.Order).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Kitchen ticket not found.");
        KitchenRules.Transition(ticket, from, to);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToTicketDto(ticket);
    }
    public Task<KitchenTicketDto> PrepareTicketAsync(int id, CancellationToken ct) => TransitionTicketAsync(id, "Queued", "Preparing", ct);
    public Task<KitchenTicketDto> ReadyTicketAsync(int id, CancellationToken ct) => TransitionTicketAsync(id, "Preparing", "Ready", ct);
}
