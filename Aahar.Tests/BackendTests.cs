using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Mapster;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aahar.Tests;

public class BackendTests
{
    [Fact]
    public void StockAndTransitionsRejectInvalidOperations()
    {
        OrderRules.RequireStock(5m, 5m, "Rice");
        Assert.Throws<DomainException>(() => OrderRules.RequireStock(4.999m, 5m, "Rice"));
        OrderRules.RequireTransition("Draft", "Draft");
        Assert.Throws<DomainException>(() => OrderRules.RequireTransition("Sent", "Draft"));
        Assert.Throws<DomainException>(() => OrderRules.RequireTransition("Paid", "Completed"));
    }

    [Fact]
    public void StockIsAggregatedAndNotDeductedIfAnyIngredientIsInsufficient()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Stock = 10m };
        var salt = new Ingredient { Id = 2, Name = "Salt", Stock = 0.1m };
        var order = new Order { Items = [new() { MenuItemId = 1, Quantity = 2 }, new() { MenuItemId = 2, Quantity = 3 }] };
        var recipe = new[] {
            new RecipeIngredient { MenuItemId = 1, IngredientId = 1, Ingredient = rice, Quantity = 2m },
            new RecipeIngredient { MenuItemId = 2, IngredientId = 1, Ingredient = rice, Quantity = 1m },
            new RecipeIngredient { MenuItemId = 2, IngredientId = 2, Ingredient = salt, Quantity = 0.1m }
        };
        Assert.Throws<DomainException>(() => OrderRules.DeductStock(order, recipe));
        Assert.Equal(10m, rice.Stock);
        Assert.Equal(0.1m, salt.Stock);
        salt.Stock = 0.3m;
        OrderRules.DeductStock(order, recipe);
        Assert.Equal(3m, rice.Stock);
        Assert.Equal(0m, salt.Stock);
    }

    [Fact]
    public void MapsterPreservesSnapshotFields()
    {
        var line = new OrderItem { MenuItemId = 4, Name = "Momo", Quantity = 2, UnitPrice = 120.5m };
        var dto = line.Adapt<OrderItemDto>();
        Assert.Equal(new OrderItemDto(4, "Momo", 2, 120.5m), dto);
        Assert.Equal(new UserDto("admin", "Admin"), new User { Username = "admin", Role = "Admin" }.Adapt<UserDto>());
    }

    [Fact]
    public async Task CookieAndCsrfProtectLoginAndMutations()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var missingToken = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "password"));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "password"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookies = login.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, c => c.Contains("__Host-aahar-auth=") && c.Contains("secure", StringComparison.OrdinalIgnoreCase) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Admin", (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Role);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task KitchenCannotAccessManagementOrCreateOrders()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kitchen", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ingredients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(1, [new(1, 1)]))).StatusCode);
    }
    private record Csrf(string RequestToken);
}

public sealed class TestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Aahar"] = "Host=localhost;Database=not_used;Username=unused;Password=unused" }));
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, LoopbackFilter>();
            services.RemoveAll<IRestaurantService>();
            services.AddScoped<IRestaurantService, FakeService>();
        });
    }
}

public sealed class LoopbackFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (ctx, following) => { ctx.Connection.RemoteIpAddress = IPAddress.Loopback; await following(); });
        next(app);
    };
}

public sealed class FakeService : IRestaurantService
{
    public Task<UserDto?> AuthenticateAsync(LoginRequest r, CancellationToken ct) => Task.FromResult<UserDto?>(
        r.Password == "password" && r.Username == "admin" ? new("admin", "Admin") :
        r.Password == "password" && r.Username == "kitchen" ? new("kitchen", "Kitchen") : null);
    public Task<UserDto> CreateUserAsync(CreateUserRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<MenuDto>> MenuAsync(bool a, CancellationToken ct) => throw new NotSupportedException();
    public Task<MenuDto> SaveMenuAsync(int? id, MenuRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<TableDto>> TablesAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<TableDto> CreateTableAsync(TableRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<IngredientDto>> IngredientsAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> CreateIngredientAsync(IngredientRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> AddStockAsync(int id, StockRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task SetRecipeAsync(int id, RecipeRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<OrderDto>>([]);
    public Task<OrderDto> CreateOrderAsync(CreateOrderRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<OrderDto> SendOrderAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task<OrderDto> PayAsync(int id, PaymentRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<SalesDto> SalesAsync(CancellationToken ct) => throw new NotSupportedException();
}
