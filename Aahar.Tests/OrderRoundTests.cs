using Aahar.Domain;
using Aahar.Application;
using Aahar.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace Aahar.Tests;

public class OrderRoundTests
{
    [Fact]
    public void OrdersProjectionPreservesSnapshotsTotalsAndTicketCount()
    {
        var date = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
        var paidAt = date.AddHours(1);
        var orders = new[]
        {
            new Order { Id = 1, Status = "Paid", CreatedAt = date, TableId = 3,
                Items = [
                    new() { Id = 2, MenuItemId = 7, Name = "New name", Quantity = 2, UnitPrice = 12, Notes = "hot" },
                    new() { Id = 1, MenuItemId = 7, Name = "Old name", Quantity = 1, UnitPrice = 10 }
                ],
                Tickets = [new(), new()], Payment = new() { Id = 4, Method = "Cash", Amount = 34, PaidAt = paidAt } },
            new Order { Id = 2, Status = "Draft", CreatedAt = date },
            new Order { Id = 3, Status = "Cancelled", CreatedAt = date.AddDays(-1), CancelledAt = paidAt,
                CancelledBy = "manager", CancellationReason = "closed", CancellationStockDisposition = "Waste" }
        };
        var result = OrderQueries.List(orders.AsQueryable()).ToList();
        Assert.Equal(2, result[0].Id);
        Assert.Equal(0, result[0].Total);
        Assert.Equal(0, result[0].RoundCount);
        Assert.Null(result[0].Payment);
        var paid = result[1];
        Assert.Equal(3, paid.TableId);
        Assert.Equal(34, paid.Total);
        Assert.Equal(2, paid.RoundCount);
        Assert.Equal(new PaymentDto(4, "Cash", 34, paidAt), paid.Payment);
        Assert.Equal(new OrderItemDto(7, "Old name", 1, 10, null), paid.Items[0]);
        Assert.Equal(new OrderItemDto(7, "New name", 2, 12, "hot"), paid.Items[1]);
        var cancelled = result[2];
        Assert.Equal(paidAt, cancelled.CancelledAt);
        Assert.Equal("manager", cancelled.CancelledBy);
        Assert.Equal("closed", cancelled.CancellationReason);
        Assert.Equal("Waste", cancelled.CancellationStockDisposition);
    }

    [Fact]
    public void OrdersProjectionUsesScalarTicketCountInSinglePostgresQuery()
    {
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var sql = OrderQueries.List(db.Orders.AsNoTracking().AsSingleQuery()).ToQueryString();
        Assert.Contains("count(*)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM \"KitchenTickets\"", sql);
        Assert.DoesNotContain("JOIN \"KitchenTickets\"", sql);
        Assert.Contains("JOIN \"OrderItem\"", sql);
        Assert.DoesNotContain("JOIN \"MenuItems\"", sql);
    }

    [Fact]
    public void RoundsPreserveSnapshotsAndMatchIdempotentRequests()
    {
        var old = new OrderItem { MenuItemId = 1, Name = "Tea", Quantity = 1, UnitPrice = 10, Notes = "old" };
        var order = new Order { Id = 9, Status = "Completed", Items = [old] };
        var menu = new Dictionary<int, MenuItem> { [1] = new() { Id = 1, Name = "New tea", Price = 20 } };
        var id = Guid.NewGuid();
        var (items, ticket) = OrderRounds.Create(order, id, [new(1, 2, "hot")], menu);
        Assert.Equal(10, old.UnitPrice);
        Assert.Equal(20, items[0].UnitPrice);
        Assert.Equal("New tea", ticket.Items[0].Name);
        Assert.True(OrderRounds.Matches(ticket, [new(1, 2, "hot")]));
        Assert.False(OrderRounds.Matches(ticket, [new(1, 2, "cold")]));
        Assert.Throws<DomainException>(() => OrderRounds.Create(order, Guid.Empty, [new(1, 2, null)], menu));
        menu[1].IsAvailable = false;
        Assert.Throws<DomainException>(() => OrderRounds.Create(order, id, [new(1, 2, null)], menu));
        Assert.Single(order.Items);
        order.Status = "Paid";
        Assert.Throws<DomainException>(() => OrderRounds.Create(order, id, [new(1, 2, null)], menu));
    }

    [Fact]
    public void AggregateRepeatedIngredientDeductionsAndCancelExactlyOnce()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Stock = 10 };
        var order = new Order { Id = 2, Status = "Sent", ConsumptionMovementCount = 0 };
        var first = new KitchenTicket { Id = 1 };
        var second = new KitchenTicket { Id = 2 };
        order.Tickets.AddRange([first, second]);
        var items = new List<OrderItem> { new() { MenuItemId = 3, Quantity = 2 } };
        var recipe = new[] { new RecipeIngredient { MenuItemId = 3, IngredientId = 1, Ingredient = rice, Quantity = 1 } };
        var a = OrderRules.DeductStock(order, items, recipe, "staff", first, false);
        var b = OrderRules.DeductStock(order, items, recipe, "staff", second, false);
        a[0].KitchenTicketId = first.Id;
        b[0].KitchenTicketId = second.Id;
        Assert.Equal(6, rice.Stock);
        Assert.Equal(2, order.ConsumptionMovementCount);
        var reversal = Assert.Single(OrderRules.Cancel(order, [.. a, .. b], "cancel", "Return", "manager", "Manager"));
        Assert.Equal(4, reversal.QuantityChange);
        Assert.Equal(10, rice.Stock);
        Assert.All(order.Tickets, x => Assert.Equal("Cancelled", x.Status));
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, [.. a, .. b], "again", "Return", "manager", "Manager"));
    }

    [Fact]
    public void EveryTicketMustBeReadyBeforeCompletion()
    {
        var order = new Order { Status = "Sent", Tickets = [new() { Status = "Ready" }, new() { Status = "Preparing" }] };
        Assert.Throws<DomainException>(() => KitchenRules.Complete(order));
        Assert.Throws<DomainException>(() => KitchenRules.RequireReady(order));
        order.Tickets[1].Status = "Ready";
        KitchenRules.Complete(order);
        Assert.Equal("Completed", order.Status);
    }

    [Fact]
    public void RetryComparisonIgnoresItemOrderButNotQuantitiesOrNotes()
    {
        var ticket = new KitchenTicket { Items = [
            new() { MenuItemId = 1, Quantity = 2, Notes = null },
            new() { MenuItemId = 2, Quantity = 3, Notes = "hot" }
        ] };
        Assert.True(OrderRounds.Matches(ticket, [new(2, 3, "hot"), new(1, 2, null)]));
        Assert.False(OrderRounds.Matches(ticket, [new(2, 3, "hot"), new(1, 1, null)]));
        Assert.False(OrderRounds.Matches(ticket, [new(2, 3, "hot"), new(1, 2, "")]));
    }

    [Fact]
    public void NewRoundsDoNotMakePreLedgerOrdersAutomaticallyCancellable()
    {
        var ingredient = new Ingredient { Id = 1, Name = "Rice", Stock = 3 };
        var order = new Order { Id = 2, Status = "Sent", ConsumptionMovementCount = null };
        var ticket = new KitchenTicket { Id = 1 };
        order.Tickets.Add(ticket);
        var movements = OrderRules.DeductStock(order, [new() { MenuItemId = 1, Quantity = 1 }],
            [new() { MenuItemId = 1, IngredientId = 1, Ingredient = ingredient, Quantity = 1 }], "staff", ticket, false);
        movements[0].KitchenTicketId = ticket.Id;
        Assert.Null(order.ConsumptionMovementCount);
        Assert.Throws<DomainException>(() => OrderRules.Cancel(order, movements, "cancel", "Return", "manager", "Manager"));
        Assert.Equal(2, ingredient.Stock);
    }

    [Fact]
    public void InsufficientStockDoesNotPartiallyDeductAndOrderLimitIsEnforced()
    {
        var ingredient = new Ingredient { Id = 1, Name = "Rice", Stock = 1 };
        var order = new Order { Status = "Sent", ConsumptionMovementCount = 1 };
        Assert.Throws<DomainException>(() => OrderRules.DeductStock(order, [new() { MenuItemId = 1, Quantity = 2 }],
            [new() { MenuItemId = 1, IngredientId = 1, Ingredient = ingredient, Quantity = 1 }], "staff", new(), false));
        Assert.Equal(1, ingredient.Stock);
        Assert.Equal(1, order.ConsumptionMovementCount);
        order.Items = Enumerable.Range(1, OrderRounds.MaxOrderLines).Select(i => new OrderItem { MenuItemId = i }).ToList();
        Assert.Throws<DomainException>(() => OrderRounds.Create(order, Guid.NewGuid(), [new(1, 1, null)],
            new Dictionary<int, MenuItem> { [1] = new() { Id = 1 } }));
    }

    [Fact]
    public async Task RoundHttpRequiresStaffRoleAndCsrfAndReturnsTicket()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var request = new AddRoundRequest(Guid.NewGuid(), [new(1, 2, "hot")]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/orders/1/rounds", request)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders/1/rounds", request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        var response = await client.PostAsJsonAsync("/api/orders/1/rounds", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AddRoundDto>())!;
        Assert.Equal(request.RequestId, result.Ticket.RequestId);
        Assert.Equal(2, result.Order.RoundCount);
        Assert.Equal("hot", Assert.Single(result.Ticket.Items).Notes);
        await using var other = new TestFactory();
        using var kitchen = other.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        kitchen.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await kitchen.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await kitchen.PostAsJsonAsync("/api/auth/login", new LoginRequest("kitchen", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await kitchen.PostAsJsonAsync("/api/orders/1/rounds", request)).StatusCode);
    }

    private record Csrf(string RequestToken);
}
