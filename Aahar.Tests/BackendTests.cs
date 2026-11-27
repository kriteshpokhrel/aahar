using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Mapster;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aahar.Tests;

public class BackendTests
{
    [Theory]
    [InlineData("23505", true)]
    [InlineData("40001", true)]
    [InlineData("40P01", true)]
    [InlineData("23503", false)]
    [InlineData("08006", false)]
    public void DatabaseConflictClassificationHandlesExecutionStrategyWrappers(string sqlState, bool expected)
    {
        var postgres = new PostgresException("Synthetic database error", "ERROR", "ERROR", sqlState);
        var update = new DbUpdateException("Save failed", postgres);
        var wrapped = new InvalidOperationException("Transient failure", update);
        Assert.Equal(expected, DatabaseConflicts.IsConflict(postgres));
        Assert.Equal(expected, DatabaseConflicts.IsConflict(update));
        Assert.Equal(expected, DatabaseConflicts.IsConflict(wrapped));
        Assert.True(DatabaseConflicts.IsConflict(new DbUpdateConcurrencyException()));
        Assert.False(DatabaseConflicts.IsConflict(new InvalidOperationException("Unrelated failure")));
    }

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
    public void DraftEditingPreservesSnapshotsAndRejectsInvalidEditsWithoutMutation()
    {
        var old = new OrderItem { MenuItemId = 1, Name = "Original", Quantity = 2, UnitPrice = 80m, Notes = "mild" };
        var order = new Order { Items = [old] };
        var menu = new Dictionary<int, MenuItem> {
            [1] = new() { Id = 1, Name = "Renamed", Price = 100m, IsAvailable = true },
            [2] = new() { Id = 2, Name = "Tea", Price = 30m, IsAvailable = true }
        };
        OrderRules.SetDraftItems(order, [new(1, 3, "spicy"), new(2, 1, null)], menu);
        Assert.Same(old, order.Items[0]);
        Assert.Equal("Original", old.Name);
        Assert.Equal(80m, old.UnitPrice);
        Assert.Equal("spicy", old.Notes);
        Assert.Equal(30m, order.Items[1].UnitPrice);
        Assert.Equal(1, order.Revision);
        menu[1].IsAvailable = false;
        Assert.Throws<DomainException>(() => OrderRules.SetDraftItems(order, [new(1, 4, "changed")], menu));
        Assert.Equal(3, old.Quantity);
        Assert.Equal("spicy", old.Notes);
        Assert.Equal(2, order.Items.Count);
        Assert.Throws<DomainException>(() => OrderRules.SetDraftItems(order, [new(1, 1, null), new(1, 1, null)], menu));
        Assert.Throws<DomainException>(() => OrderRules.SetDraftItems(order, [new(1, 1, new string('x', 301))], menu));
        OrderRules.SetDraftItems(order, [new(1, 2, null)], menu);
        Assert.Single(order.Items);
        Assert.Equal(80m, order.Items[0].UnitPrice);
        order.Status = "Sent";
        Assert.Throws<DomainException>(() => OrderRules.SetDraftItems(order, [new(1, 1, null)], menu));
    }

    [Fact]
    public void TicketSnapshotsAndTransitionsEnforceKitchenWorkflow()
    {
        var order = new Order { Id = 12, Items = [new() { MenuItemId = 4, Name = "Momo", Quantity = 2, UnitPrice = 120m, Notes = "mild" }] };
        var ticket = KitchenRules.CreateTicket(order);
        ticket.Order = order;
        order.Tickets.Add(ticket);
        order.Items[0].Name = "Changed";
        order.Items[0].Notes = "hot";
        Assert.Equal("Momo", ticket.Items[0].Name);
        Assert.Equal("mild", ticket.Items[0].Notes);
        Assert.Equal(new KitchenTicketItemDto(4, "Momo", 2, 120m, "mild"), ticket.Items[0].Adapt<KitchenTicketItemDto>());
        order.Status = "Sent";
        Assert.Throws<DomainException>(() => KitchenRules.Complete(order));
        Assert.Throws<DomainException>(() => KitchenRules.Transition(ticket, "Preparing", "Ready"));
        KitchenRules.Transition(ticket, "Queued", "Preparing");
        Assert.Throws<DomainException>(() => KitchenRules.Transition(ticket, "Queued", "Preparing"));
        KitchenRules.Transition(ticket, "Preparing", "Ready");
        KitchenRules.Complete(order);
        Assert.Equal("Completed", order.Status);
        Assert.Throws<DomainException>(() => KitchenRules.Transition(ticket, "Preparing", "Ready"));
    }

    [Fact]
    public void DraftLineReplacementTracksNewAndRemovedItemsWithoutDatabaseAccess()
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options;
        using var db = new RestaurantDbContext(options);
        var removed = new OrderItem { Id = 9, OrderId = 5, MenuItemId = 1, Name = "Old", Quantity = 1, UnitPrice = 10 };
        var order = new Order { Id = 5, Items = [removed] };
        db.Attach(order);
        var oldItems = order.Items.ToArray();
        OrderRules.SetDraftItems(order, [new(2, 2, "no onions")], new Dictionary<int, MenuItem> { [2] = new() { Id = 2, Name = "New", Price = 25 } });
        db.Set<OrderItem>().RemoveRange(oldItems.Except(order.Items));
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Deleted, db.Entry(removed).State);
        Assert.Equal(EntityState.Added, db.Entry(Assert.Single(order.Items)).State);
        Assert.Equal(EntityState.Modified, db.Entry(order).State);
    }

    [Fact]
    public void CancellationClosesTheKitchenTicket()
    {
        var ticket = new KitchenTicket { Status = "Preparing" };
        var order = new Order { Id = 4, Status = "Sent", ConsumptionMovementCount = 0, Tickets = [ticket] };
        OrderRules.Cancel(order, [], "changed mind", "Waste", "manager", "Manager");
        Assert.Equal("Cancelled", ticket.Status);
        Assert.Equal("Cancelled", order.Status);
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
        Assert.Throws<DomainException>(() => OrderRules.DeductStock(order, recipe, "staff"));
        Assert.Equal(10m, rice.Stock);
        Assert.Equal(0.1m, salt.Stock);
        salt.Stock = 0.3m;
        var movements = OrderRules.DeductStock(order, recipe, "staff");
        Assert.Equal(3m, rice.Stock);
        Assert.Equal(0m, salt.Stock);
        Assert.Equal(2, order.ConsumptionMovementCount);
        Assert.Contains(movements, x => x.IngredientId == 1 && x.QuantityChange == -7m && x.BalanceAfter == 3m && x.Actor == "staff");
    }

    [Fact]
    public void CancellationReversesRecordedDeductionsOnceAndRequiresAuthorization()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Stock = 3m };
        var order = new Order { Id = 17, Status = "Sent", ConsumptionMovementCount = 1 };
        var consumed = new[] { new StockMovement { IngredientId = 1, Ingredient = rice, OrderId = 17, Kind = "Consumption", QuantityChange = -7m } };
        Assert.Throws<ForbiddenException>(() => OrderRules.Cancel(order, consumed, "mistake", "Return", "waiter", "Staff"));
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, consumed, " ", "Return", "manager", "Manager"));
        Assert.Equal(3m, rice.Stock);
        var reversal = Assert.Single(OrderRules.Cancel(order, consumed, " mistake ", "Return", "manager", "Manager"));
        Assert.Equal(10m, rice.Stock);
        Assert.Equal(7m, reversal.QuantityChange);
        Assert.Equal("Cancellation", reversal.Kind);
        Assert.Equal("manager", reversal.Actor);
        Assert.Equal("mistake", order.CancellationReason);
        Assert.Equal("Return", order.CancellationStockDisposition);
        Assert.NotNull(order.CancelledAt);
        Assert.Equal("Cancelled", order.Status);
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, consumed, "retry", "Return", "manager", "Manager"));
        Assert.Equal(10m, rice.Stock);
    }

    [Fact]
    public void CancellationRejectsMissingLedgerAndOverflowWithoutChangingStock()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Stock = 99999999999.999m };
        var order = new Order { Id = 5, Status = "Completed", ConsumptionMovementCount = 1 };
        var consumed = new[] { new StockMovement { IngredientId = 1, Ingredient = rice, OrderId = 5, Kind = "Consumption", QuantityChange = -1m } };
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, [], "reason", "Return", "admin", "Admin"));
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, consumed, "reason", "Return", "admin", "Admin"));
        Assert.Equal(99999999999.999m, rice.Stock);
        Assert.Equal("Completed", order.Status);
        order.ConsumptionMovementCount = null;
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, [], "reason", "Waste", "admin", "Admin"));
        order.Status = "Paid";
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, [], "reason", "Waste", "admin", "Admin"));
        order.Status = "Draft";
        Assert.Empty(OrderRules.Cancel(order, [], "reason", "None", "staff", "Staff"));
        Assert.Equal("Cancelled", order.Status);
    }

    [Fact]
    public void WasteCancellationPreservesStockAndRequiresExplicitDisposition()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Stock = 3m };
        var order = new Order { Id = 5, Status = "Completed", ConsumptionMovementCount = 1 };
        var consumed = new[] { new StockMovement { IngredientId = 1, Ingredient = rice, OrderId = 5, Kind = "Consumption", QuantityChange = -7m } };
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, consumed, "reason", "None", "admin", "Admin"));
        Assert.Equal(3m, rice.Stock);
        Assert.Empty(OrderRules.Cancel(order, consumed, "spoiled", "Waste", "admin", "Admin"));
        Assert.Equal(3m, rice.Stock);
        Assert.Equal("Waste", order.CancellationStockDisposition);
    }

    [Fact]
    public void ModelHasUniqueActiveTableAndOrderMovementConstraints()
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options;
        using var db = new RestaurantDbContext(options);
        var index = db.Model.FindEntityType(typeof(Order))!.GetIndexes().Single(x => x.GetDatabaseName() == "IX_Orders_ActiveTable");
        Assert.True(index.IsUnique);
        Assert.Equal("\"TableId\" IS NOT NULL AND \"Status\" IN ('Draft', 'Sent', 'Completed')", index.GetFilter());
        Assert.True(db.Model.FindEntityType(typeof(Order))!.FindProperty("TableId")!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(Order))!.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(KitchenTicket))!.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["OrderId", "RequestId"])).IsUnique);
        var movements = db.Model.FindEntityType(typeof(StockMovement))!.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["OrderId", "IngredientId", "Kind"]));
        Assert.True(movements.IsUnique);
        Assert.Equal("\"OrderId\" IS NOT NULL AND \"KitchenTicketId\" IS NULL", movements.GetFilter());
    }
    [Fact]
    public void MapsterPreservesSnapshotFields()
    {
        var line = new OrderItem { MenuItemId = 4, Name = "Momo", Quantity = 2, UnitPrice = 120.5m };
        var dto = line.Adapt<OrderItemDto>();
        Assert.Equal(new OrderItemDto(4, "Momo", 2, 120.5m, null), dto);
        Assert.Equal(new UserDto("admin", "Admin"), new User { Username = "admin", Role = "Admin" }.Adapt<UserDto>());
        var movement = new StockMovement { Id = 7, IngredientId = 4, OrderId = 3, Kind = "Consumption", QuantityChange = -2m, BalanceAfter = 8m, Actor = "staff" };
        Assert.Equal(new StockMovementDto(7, 4, 3, "Consumption", -2m, 8m, "staff", movement.OccurredAt, null, null), movement.Adapt<StockMovementDto>());
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
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/orders/1/cancel", new CancelOrderRequest("mistake", "None"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/orders/1/items", new EditOrderItemsRequest([new(1, 1)]))).StatusCode);
    }

    [Fact]
    public async Task StaffMayCancelDraftButNotSubmittedOrder()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await client.PostAsJsonAsync("/api/orders/1/cancel", new CancelOrderRequest("mistake", "None"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("staff", cancelled!.CancelledBy);
        Assert.Equal("None", cancelled.CancellationStockDisposition);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/orders/2/cancel", new CancelOrderRequest("mistake", "Return"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/kitchen/tickets/1/prepare", null)).StatusCode);
    }

    [Fact]
    public async Task TakeawayAndNotesRoundTripThroughHttp()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(null, [new(5, 2, "no onions")]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Null(order!.TableId);
        Assert.Equal("no onions", Assert.Single(order.Items).Notes);
        response = await client.PutAsJsonAsync("/api/orders/7/items", new EditOrderItemsRequest([new(5, 1, "mild")]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("mild", Assert.Single(order!.Items).Notes);
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
            services.RemoveAll<IGuestRequestService>();
            services.AddScoped<IGuestRequestService, FakeGuestService>();
            services.RemoveAll<IReportService>();
            services.AddScoped<IReportService, FakeReportService>();
            services.RemoveAll<IInventoryService>();
            services.AddScoped<IInventoryService, FakeInventoryService>();
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
        r.Password == "password" && r.Username == "kitchen" ? new("kitchen", "Kitchen") :
        r.Password == "password" && r.Username == "manager" ? new("manager", "Manager") :
        r.Password == "password" && r.Username == "staff" ? new("staff", "Staff") : null);
    public Task<UserDto> CreateUserAsync(CreateUserRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<MenuDto>> MenuAsync(bool a, CancellationToken ct) => throw new NotSupportedException();
    public Task<MenuDto> SaveMenuAsync(int? id, MenuRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<TableDto>> TablesAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<TableDto> CreateTableAsync(TableRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<IngredientDto>> IngredientsAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> CreateIngredientAsync(IngredientRequest r, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> AddStockAsync(int id, StockRequest r, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<StockMovementDto>> StockMovementsAsync(int id, long? beforeId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<RecipeRequest> RecipeAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task SetRecipeAsync(int id, RecipeRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OrderDto>> OrdersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<OrderDto>>([]);
    public Task<OrderDto> CreateOrderAsync(CreateOrderRequest r, CancellationToken ct) =>
        Task.FromResult(new OrderDto(7, r.TableId, "Draft", DateTime.UtcNow, r.Items.Select(x => new OrderItemDto(x.MenuItemId, "Item", x.Quantity, 10, x.Notes)).ToList(), 20, null, null, null, null, null));
    public Task<OrderDto> EditOrderItemsAsync(int id, EditOrderItemsRequest r, CancellationToken ct) =>
        CreateOrderAsync(new CreateOrderRequest(null, r.Items), ct);
    public Task<OrderDto> SendOrderAsync(int id, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<AddRoundDto> AddRoundAsync(int id, AddRoundRequest request, string actor, CancellationToken ct)
    {
        var items = request.Items.Select(x => new OrderItemDto(x.MenuItemId, "Item", x.Quantity, 10, x.Notes)).ToList();
        return Task.FromResult(new AddRoundDto(
            new OrderDto(id, 1, "Sent", DateTime.UtcNow, items, items.Sum(x => x.UnitPrice * x.Quantity), null, null, null, null, null, 2),
            new KitchenTicketDto(10, id, 1, "Queued", DateTime.UtcNow, false,
                items.Select(x => new KitchenTicketItemDto(x.MenuItemId, x.Name, x.Quantity, x.UnitPrice, x.Notes)).ToList(), request.RequestId)));
    }
    public Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest r, string actor, string role, CancellationToken ct)
    {
        var order = new Order { Id = id, Status = id == 1 ? "Draft" : "Sent", ConsumptionMovementCount = id == 1 ? null : 0 };
        OrderRules.Cancel(order, [], r.Reason, r.StockDisposition, actor, role);
        return Task.FromResult(new OrderDto(id, 1, order.Status, order.CreatedAt, [], 0, null, order.CancelledAt, order.CancelledBy, order.CancellationReason, order.CancellationStockDisposition));
    }
    public Task<OrderDto> CompleteOrderAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task<OrderDto> PayAsync(int id, PaymentRequest r, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<KitchenTicketDto>> KitchenTicketsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<KitchenTicketDto>>([]);
    public Task<KitchenTicketDto> PrepareTicketAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task<KitchenTicketDto> ReadyTicketAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    public Task<SalesDto> SalesAsync(CancellationToken ct) => throw new NotSupportedException();
}
