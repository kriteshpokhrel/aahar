namespace Aahar.Application;

public record LoginRequest(string Username, string Password);
public record UserDto(string Username, string Role);
public record CreateUserRequest(string Username, string Password, string Role);
public record MenuRequest(string Name, string? Description, string Category, decimal Price, bool IsAvailable);
public record MenuDto(int Id, string Name, string? Description, string Category, decimal Price, bool IsAvailable);
public record TableRequest(int Number, int Capacity);
public record TableDto(int Id, int Number, int Capacity);
public record IngredientRequest(string Name, string Unit, decimal Stock);
public record IngredientDto(int Id, string Name, string Unit, decimal Stock);
public record StockRequest(decimal Quantity);
public record RecipeLine(int IngredientId, decimal Quantity);
public record RecipeRequest(List<RecipeLine> Ingredients);
public record OrderLineRequest(int MenuItemId, int Quantity);
public record CreateOrderRequest(int TableId, List<OrderLineRequest> Items);
public record OrderItemDto(int MenuItemId, string Name, int Quantity, decimal UnitPrice);
public record PaymentDto(int Id, string Method, decimal Amount, DateTime PaidAt);
public record OrderDto(int Id, int TableId, string Status, DateTime CreatedAt, List<OrderItemDto> Items, decimal Total, PaymentDto? Payment);
public record PaymentRequest(string Method);
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
    Task<IngredientDto> CreateIngredientAsync(IngredientRequest request, CancellationToken ct);
    Task<IngredientDto> AddStockAsync(int id, StockRequest request, CancellationToken ct);
    Task SetRecipeAsync(int menuId, RecipeRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderDto> SendOrderAsync(int id, CancellationToken ct);
    Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct);
    Task<OrderDto> PayAsync(int id, PaymentRequest request, CancellationToken ct);
    Task<SalesDto> SalesAsync(CancellationToken ct);
}
