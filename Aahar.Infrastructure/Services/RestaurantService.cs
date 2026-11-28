using Aahar.Application;
using Aahar.Domain;
using Mapster;
using Microsoft.AspNetCore.Identity;

namespace Aahar.Infrastructure;

public sealed class RestaurantService(IMenuRepository menuRepository, IOrderRepository orders,
    IInventoryRepository inventory, IUserRepository users, IUnitOfWork unitOfWork, IPasswordHasher<User> hasher) : IRestaurantService
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new DomainException(message);
    }
    private static bool ValidText(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool Money(decimal n) => n >= 0 && n <= 9999999999.99m && decimal.Round(n, 2) == n;
    private static bool Quantity(decimal n) => InventoryRules.ValidQuantity(n);
    internal static IngredientDto ToDto(Ingredient ingredient) =>
        new(ingredient.Id, ingredient.Name, ingredient.Unit, ingredient.Stock, ingredient.LowStockThreshold, ingredient.IsLowStock);
    internal static OrderDto ToDto(Order order) => new(order.Id, order.TableId, order.Status, order.CreatedAt,
        order.Items.Adapt<List<OrderItemDto>>(), order.Items.Sum(x => x.UnitPrice * x.Quantity), order.Payment?.Adapt<PaymentDto>(),
        order.CancelledAt, order.CancelledBy, order.CancellationReason, order.CancellationStockDisposition, order.Tickets.Count);
    private static KitchenTicketDto ToTicketDto(KitchenTicket ticket) => new(ticket.Id, ticket.OrderId, ticket.Order.TableId,
        ticket.Status, ticket.CreatedAt, ticket.IsReconstructed, ticket.Items.Adapt<List<KitchenTicketItemDto>>(), ticket.RequestId);
    private static List<DraftLine> DraftLines(List<OrderLineRequest>? items)
    {
        Check(items is { Count: > 0 and <= 100 } && items.All(x => x is not null), "Invalid order items.");
        return items!.Select(x => new DraftLine(x.MenuItemId, x.Quantity, x.Notes)).ToList();
    }
    private async Task<Dictionary<int, MenuItem>> MenuForLines(List<DraftLine> lines, CancellationToken ct)
    {
        var ids = lines.Select(x => x.MenuItemId).ToArray();
        return await menuRepository.FindByIdsAsync(ids, ct);
    }

    public async Task<UserDto?> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        if (!ValidText(request.Username, 100) || request.Password is not { Length: > 0 and <= 256 }) return null;
        var user = await users.FindByUsernameAsync(request.Username.Trim().ToLowerInvariant(), ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;
        return user.Adapt<UserDto>();
    }
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        Check(ValidText(request.Username, 100) && request.Password is { Length: >= 12 and <= 256 } &&
            request.Role is "Admin" or "Manager" or "Staff" or "Kitchen", "Invalid username, password, or role.");
        var user = new User { Username = request.Username.Trim().ToLowerInvariant(), Role = request.Role };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        users.Add(user); await unitOfWork.SaveChangesAsync(ct); return user.Adapt<UserDto>();
    }
    public async Task<IReadOnlyList<MenuDto>> MenuAsync(bool availableOnly, CancellationToken ct) =>
        (await menuRepository.ListAsync(availableOnly, ct)).Adapt<List<MenuDto>>();
    public async Task<MenuDto> SaveMenuAsync(int? id, MenuRequest r, CancellationToken ct)
    {
        Check(ValidText(r.Name, 100) && ValidText(r.Category, 100) && (r.Description?.Length ?? 0) <= 300 && Money(r.Price), "Invalid menu item.");
        var item = id is null ? new MenuItem() : await menuRepository.FindAsync(id.Value, ct) ?? throw new DomainException("Menu item not found.");
        item.Name = r.Name.Trim(); item.Category = r.Category.Trim(); item.Description = r.Description; item.Price = r.Price; item.IsAvailable = r.IsAvailable;
        if (id is null) menuRepository.Add(item);
        await unitOfWork.SaveChangesAsync(ct);
        return item.Adapt<MenuDto>();
    }
    public async Task<IReadOnlyList<TableDto>> TablesAsync(CancellationToken ct) =>
        await orders.TablesAsync(ct);
    public async Task<TableDto> CreateTableAsync(TableRequest r, CancellationToken ct)
    {
        Check(r.Number > 0 && r.Capacity is > 0 and <= 100, "Invalid table.");
        var table = new DiningTable { Number = r.Number, Capacity = r.Capacity };
        orders.AddTable(table); await unitOfWork.SaveChangesAsync(ct); return new TableDto(table.Id, table.Number, table.Capacity, false);
    }
    public async Task<IReadOnlyList<IngredientDto>> IngredientsAsync(CancellationToken ct) =>
        (await inventory.IngredientsAsync(false, ct)).Select(ToDto).ToList();
    public async Task<IngredientDto> CreateIngredientAsync(IngredientRequest r, string actor, CancellationToken ct)
    {
        Check(ValidText(r.Name, 100) && ValidText(r.Unit, 30) && Quantity(r.Stock) && Quantity(r.LowStockThreshold), "Invalid ingredient.");
        var ingredient = new Ingredient { Name = r.Name.Trim(), Unit = r.Unit.Trim(), Stock = r.Stock };
        inventory.AddIngredient(ingredient);
        inventory.AddThresholdChange(InventoryRules.ChangeThreshold(ingredient, r.LowStockThreshold, "Initial threshold", actor, initial: true));
        inventory.AddMovement(new StockMovement { Ingredient = ingredient, Kind = "Opening", QuantityChange = r.Stock, BalanceAfter = r.Stock, Actor = actor, Reason = "Opening balance" });
        await unitOfWork.SaveChangesAsync(ct); return ToDto(ingredient);
    }
    public async Task<IngredientDto> AddStockAsync(int id, StockRequest r, string actor, CancellationToken ct)
    {
        Check(Quantity(r.Quantity) && r.Quantity > 0, "Stock quantity must be positive with at most three decimal places.");
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var ingredient = await inventory.FindIngredientAsync(id, ct) ?? throw new DomainException("Ingredient not found.");
        Check(r.Unit is null || r.Unit == ingredient.Unit, "Unit does not match the ingredient.");
        var reason = r.Reason is null ? "Manual stock receipt" : InventoryRules.RequiredText(r.Reason, 300, "Reason");
        Check(Quantity(ingredient.Stock + r.Quantity), "Stock exceeds the maximum.");
        ingredient.Stock += r.Quantity;
        inventory.AddMovement(new StockMovement { IngredientId = id, Kind = "Receipt", QuantityChange = r.Quantity, BalanceAfter = ingredient.Stock, Actor = actor, Reason = reason });
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(ingredient);
    }
    public async Task<IReadOnlyList<StockMovementDto>> StockMovementsAsync(int id, long? beforeId, int limit, CancellationToken ct)
    {
        Check(limit is > 0 and <= 100 && (beforeId is null || beforeId > 0), "Invalid movement page.");
        Check(await inventory.IngredientExistsAsync(id, ct), "Ingredient not found.");
        return (await inventory.MovementsAsync(id, beforeId, limit, ct)).Adapt<List<StockMovementDto>>();
    }
    public async Task<RecipeRequest> RecipeAsync(int menuId, CancellationToken ct)
    {
        Check(await menuRepository.ExistsAsync(menuId, false, ct), "Menu item not found.");
        var ingredients = await menuRepository.RecipeAsync(menuId, ct);
        return new RecipeRequest(ingredients);
    }
    public async Task SetRecipeAsync(int menuId, RecipeRequest r, CancellationToken ct)
    {
        Check(r.Ingredients is not null && r.Ingredients.Count <= 100 && r.Ingredients.All(x => x is not null && x.IngredientId > 0 && Quantity(x.Quantity) && x.Quantity > 0) &&
            r.Ingredients.Select(x => x.IngredientId).Distinct().Count() == r.Ingredients.Count, "Invalid recipe.");
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var item = await menuRepository.FindWithRecipeAsync(menuId, ct) ?? throw new DomainException("Menu item not found.");
        var ids = r.Ingredients!.Select(x => x.IngredientId).ToArray();
        Check(await inventory.CountIngredientsAsync(ids, ct) == ids.Length, "Ingredient not found.");
        menuRepository.RemoveRecipes(item.Recipe);
        await unitOfWork.SaveChangesAsync(ct);
        menuRepository.AddRecipes(r.Ingredients!.Select(x => new RecipeIngredient { MenuItemId = menuId, IngredientId = x.IngredientId, Quantity = x.Quantity }));
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct) =>
        await orders.ListAsync(ct);
    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest r, CancellationToken ct)
    {
        Check(r.TableId is null or > 0, "Invalid table.");
        var lines = DraftLines(r.Items);
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        if (r.TableId is int tableId) Check(await orders.TableExistsAsync(tableId, ct), "Table not found.");
        var order = new Order { TableId = r.TableId };
        OrderRules.SetDraftItems(order, lines, await MenuForLines(lines, ct));
        orders.Add(order); await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    private async Task<Order> GetOrder(int id, CancellationToken ct) =>
        await orders.FindAsync(id, ct) ?? throw new DomainException("Order not found.");
    public async Task<OrderDto> EditOrderItemsAsync(int id, EditOrderItemsRequest r, CancellationToken ct)
    {
        var lines = DraftLines(r.Items);
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct);
        var oldItems = order.Items.ToArray();
        OrderRules.SetDraftItems(order, lines, await MenuForLines(lines, ct));
        orders.RemoveItems(oldItems.Except(order.Items));
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> SendOrderAsync(int id, string actor, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct);
        OrderRules.RequireTransition(order.Status, "Draft");
        var ids = order.Items.Select(x => x.MenuItemId).ToArray();
        var recipes = await menuRepository.RecipesForMenuAsync(ids, ct);
        var ticket = KitchenRules.CreateTicket(order);
        order.Tickets.Add(ticket);
        inventory.AddMovements(OrderRules.DeductStock(order, order.Items, recipes, actor, ticket, initial: true));
        OrderRules.Touch(order);
        order.Status = "Sent";
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<AddRoundDto> AddRoundAsync(int id, AddRoundRequest r, string actor, CancellationToken ct)
    {
        var lines = DraftLines(r.Items);
        Check(r.RequestId != Guid.Empty, "A nonempty request ID is required.");
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct);
        var existing = order.Tickets.SingleOrDefault(x => x.RequestId == r.RequestId);
        if (existing is not null)
        {
            if (!OrderRounds.Matches(existing, lines)) throw new ConflictException("Request ID was used with different items.");
            await tx.CommitAsync(ct);
            return new AddRoundDto(ToDto(order), ToTicketDto(existing));
        }
        var ids = lines.Select(x => x.MenuItemId).Distinct().Order().ToArray();
        var menu = await menuRepository.LockForShareAsync(ids, ct);
        var (items, ticket) = OrderRounds.Create(order, r.RequestId, lines, menu);
        var recipes = await menuRepository.RecipesForMenuAsync(ids, ct);
        var movements = OrderRules.DeductStock(order, items, recipes, actor, ticket, initial: false);
        order.Items.AddRange(items);
        order.Tickets.Add(ticket);
        inventory.AddMovements(movements);
        OrderRules.Touch(order);
        order.Status = "Sent";
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new AddRoundDto(ToDto(order), ToTicketDto(ticket));
    }
    public async Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest r, string actor, string role, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct);
        var consumed = await inventory.ConsumptionAsync(id, ct);
        inventory.AddMovements(OrderRules.Cancel(order, consumed, r.Reason, r.StockDisposition, actor, role));
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct); KitchenRules.Complete(order);
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<OrderDto> PayAsync(int id, PaymentRequest r, CancellationToken ct)
    {
        Check(r.Method is "Cash" or "Card", "Payment method must be Cash or Card.");
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var order = await GetOrder(id, ct);
        if (order.Status == "Sent") KitchenRules.RequireReady(order);
        else OrderRules.RequireTransition(order.Status, "Completed");
        Check(order.Payment is null, "Order is already paid.");
        OrderRules.Touch(order);
        order.Payment = new Payment { Method = r.Method, Amount = order.Items.Sum(x => x.UnitPrice * x.Quantity) };
        order.Status = "Paid"; await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToDto(order);
    }
    public async Task<SalesDto> SalesAsync(CancellationToken ct) =>
        await orders.SalesAsync(ct);
    public async Task<IReadOnlyList<KitchenTicketDto>> KitchenTicketsAsync(CancellationToken ct) =>
        (await orders.TicketsAsync(ct)).Select(ToTicketDto).ToList();
    private async Task<KitchenTicketDto> TransitionTicketAsync(int id, string from, string to, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var ticket = await orders.FindTicketAsync(id, ct)
            ?? throw new DomainException("Kitchen ticket not found.");
        KitchenRules.Transition(ticket, from, to);
        await unitOfWork.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ToTicketDto(ticket);
    }
    public Task<KitchenTicketDto> PrepareTicketAsync(int id, CancellationToken ct) => TransitionTicketAsync(id, "Queued", "Preparing", ct);
    public Task<KitchenTicketDto> ReadyTicketAsync(int id, CancellationToken ct) => TransitionTicketAsync(id, "Preparing", "Ready", ct);
}
