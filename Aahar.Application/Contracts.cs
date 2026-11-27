namespace Aahar.Application;

public record LoginRequest(string Username, string Password);
public record UserDto(string Username, string Role);
public record CreateUserRequest(string Username, string Password, string Role);
public record MenuRequest(string Name, string? Description, string Category, decimal Price, bool IsAvailable);
public record MenuDto(int Id, string Name, string? Description, string Category, decimal Price, bool IsAvailable);
public record TableRequest(int Number, int Capacity);
public record TableDto(int Id, int Number, int Capacity, bool IsOccupied);
public record IngredientRequest(string Name, string Unit, decimal Stock, decimal LowStockThreshold = 0);
public record IngredientDto(int Id, string Name, string Unit, decimal Stock, decimal LowStockThreshold, bool IsLowStock);
public record StockRequest(decimal Quantity, string? Unit = null, string? Reason = null);
public record StockMovementDto(long Id, int IngredientId, int? OrderId, string Kind, decimal QuantityChange, decimal BalanceAfter, string Actor, DateTime OccurredAt, string? Reason, int? PurchaseReceiptLineId);
public record RecipeLine(int IngredientId, decimal Quantity);
public record RecipeRequest(List<RecipeLine> Ingredients);
public record OrderLineRequest(int MenuItemId, int Quantity, string? Notes = null);
public record CreateOrderRequest(int? TableId, List<OrderLineRequest> Items);
public record EditOrderItemsRequest(List<OrderLineRequest> Items);
public record OrderItemDto(int MenuItemId, string Name, int Quantity, decimal UnitPrice, string? Notes);
public record PaymentDto(int Id, string Method, decimal Amount, DateTime PaidAt);
public record OrderDto(int Id, int? TableId, string Status, DateTime CreatedAt, List<OrderItemDto> Items, decimal Total, PaymentDto? Payment,
    DateTime? CancelledAt, string? CancelledBy, string? CancellationReason, string? CancellationStockDisposition, int RoundCount = 0);
public record KitchenTicketItemDto(int MenuItemId, string Name, int Quantity, decimal UnitPrice, string? Notes);
public record KitchenTicketDto(int Id, int OrderId, int? TableId, string Status, DateTime CreatedAt, bool IsReconstructed, List<KitchenTicketItemDto> Items, Guid? RequestId = null);
public record AddRoundRequest(Guid RequestId, List<OrderLineRequest> Items);
public record AddRoundDto(OrderDto Order, KitchenTicketDto Ticket);
public record PaymentRequest(string Method);
public record CancelOrderRequest(string Reason, string StockDisposition);
public record SalesDto(int OrdersPaid, decimal Total);

public interface IRestaurantService
{
    Task<UserDto?> AuthenticateAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
    Task<IReadOnlyList<MenuDto>> MenuAsync(bool availableOnly, CancellationToken ct);
    Task<MenuDto> SaveMenuAsync(int? id, MenuRequest request, CancellationToken ct);
    Task<IReadOnlyList<TableDto>> TablesAsync(CancellationToken ct);
    Task<TableDto> CreateTableAsync(TableRequest request, CancellationToken ct);
    Task<IReadOnlyList<IngredientDto>> IngredientsAsync(CancellationToken ct);
    Task<IngredientDto> CreateIngredientAsync(IngredientRequest request, string actor, CancellationToken ct);
    Task<IngredientDto> AddStockAsync(int id, StockRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<StockMovementDto>> StockMovementsAsync(int id, long? beforeId, int limit, CancellationToken ct);
    Task<RecipeRequest> RecipeAsync(int menuId, CancellationToken ct);
    Task SetRecipeAsync(int menuId, RecipeRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderDto> EditOrderItemsAsync(int id, EditOrderItemsRequest request, CancellationToken ct);
    Task<OrderDto> SendOrderAsync(int id, string actor, CancellationToken ct);
    Task<AddRoundDto> AddRoundAsync(int id, AddRoundRequest request, string actor, CancellationToken ct);
    Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest request, string actor, string role, CancellationToken ct);
    Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct);
    Task<OrderDto> PayAsync(int id, PaymentRequest request, CancellationToken ct);
    Task<IReadOnlyList<KitchenTicketDto>> KitchenTicketsAsync(CancellationToken ct);
    Task<KitchenTicketDto> PrepareTicketAsync(int id, CancellationToken ct);
    Task<KitchenTicketDto> ReadyTicketAsync(int id, CancellationToken ct);
    Task<SalesDto> SalesAsync(CancellationToken ct);
}
