using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aahar.Tests;

public sealed class OptInRoundsFactAttribute : FactAttribute
{
    public OptInRoundsFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AAHAR_RUN_POSTGRES_ROUND_TESTS") != "1")
            Skip = "Requires explicit opt-in and an approved dedicated aahar_test database.";
    }
}

[Collection("PostgreSQL")]
public class PostgresRoundTests
{
    [OptInRoundsFact]
    public async Task CancellationRacingRoundReturnsAllCommittedStockExactlyOnce()
    {
        var connection = Environment.GetEnvironmentVariable("AAHAR_ROUNDS_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set AAHAR_ROUNDS_TEST_CONNECTION privately for the approved test database.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "aahar_test")
            throw new InvalidOperationException("Tests require the dedicated aahar_test database.");
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql(connection).Options;
        int menuId, ingredientId, orderId;
        await using (var db = new RestaurantDbContext(options))
        {
            var ingredient = new Ingredient { Name = "cancel-race-" + Guid.NewGuid().ToString("N"), Unit = "kg", Stock = 10 };
            var menu = new MenuItem { Name = ingredient.Name, Category = "test", Price = 5, IsAvailable = true,
                Recipe = [new RecipeIngredient { Ingredient = ingredient, Quantity = 1 }] };
            db.MenuItems.Add(menu);
            await db.SaveChangesAsync();
            menuId = menu.Id;
            ingredientId = ingredient.Id;
            var service = new RestaurantService(db, new PasswordHasher<User>());
            orderId = (await service.CreateOrderAsync(new(null, [new(menuId, 1)]), default)).Id;
            await service.SendOrderAsync(orderId, "test", default);
        }

        async Task Attempt(Func<RestaurantService, Task> action)
        {
            await using var db = new RestaurantDbContext(options);
            try { await action(new RestaurantService(db, new PasswordHasher<User>())); }
            catch (DomainException) { }
            catch (Exception e) when (DatabaseConflicts.IsConflict(e)) { }
        }
        await Task.WhenAll(
            Attempt(async service => { await service.CancelOrderAsync(orderId, new("cancel", "Return"), "test", "Manager", default); }),
            Attempt(async service => { await service.AddRoundAsync(orderId, new(Guid.NewGuid(), [new(menuId, 2)]), "test", default); }));

        await using (var db = new RestaurantDbContext(options))
        {
            if ((await db.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId)).Status != "Cancelled")
                await new RestaurantService(db, new PasswordHasher<User>()).CancelOrderAsync(orderId, new("cancel", "Return"), "test", "Manager", default);
        }
        await using var verify = new RestaurantDbContext(options);
        var order = await verify.Orders.Include(x => x.Tickets).SingleAsync(x => x.Id == orderId);
        Assert.Equal("Cancelled", order.Status);
        Assert.All(order.Tickets, x => Assert.Equal("Cancelled", x.Status));
        Assert.Equal(10, (await verify.Ingredients.SingleAsync(x => x.Id == ingredientId)).Stock);
        var movements = await verify.StockMovements.Where(x => x.OrderId == orderId).ToListAsync();
        Assert.Equal(order.Tickets.Count, movements.Count(x => x.Kind == "Consumption"));
        Assert.Equal(-movements.Where(x => x.Kind == "Consumption").Sum(x => x.QuantityChange),
            Assert.Single(movements, x => x.Kind == "Cancellation").QuantityChange);
    }

    [OptInRoundsFact]
    public async Task PaymentRacingNewRoundCannotUnderchargeOrSendAfterPayment()
    {
        var connection = Environment.GetEnvironmentVariable("AAHAR_ROUNDS_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set AAHAR_ROUNDS_TEST_CONNECTION privately for the approved test database.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "aahar_test")
            throw new InvalidOperationException("Tests require the dedicated aahar_test database.");
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql(connection).Options;
        int menuId, orderId;
        await using (var db = new RestaurantDbContext(options))
        {
            var menu = new MenuItem { Name = "payment-race-" + Guid.NewGuid().ToString("N"), Category = "test", Price = 5, IsAvailable = true };
            db.MenuItems.Add(menu);
            await db.SaveChangesAsync();
            menuId = menu.Id;
            var service = new RestaurantService(db, new PasswordHasher<User>());
            orderId = (await service.CreateOrderAsync(new(null, [new(menuId, 1)]), default)).Id;
            await service.SendOrderAsync(orderId, "test", default);
            var ticket = (await service.KitchenTicketsAsync(default)).Single(x => x.OrderId == orderId);
            await service.PrepareTicketAsync(ticket.Id, default);
            await service.ReadyTicketAsync(ticket.Id, default);
        }

        async Task Attempt(Func<RestaurantService, Task> action)
        {
            await using var db = new RestaurantDbContext(options);
            try { await action(new RestaurantService(db, new PasswordHasher<User>())); }
            catch (DomainException) { }
            catch (Exception e) when (DatabaseConflicts.IsConflict(e)) { }
        }
        await Task.WhenAll(
            Attempt(async service => { await service.PayAsync(orderId, new("Cash"), default); }),
            Attempt(async service => { await service.AddRoundAsync(orderId, new(Guid.NewGuid(), [new(menuId, 1)]), "test", default); }));

        await using var verify = new RestaurantDbContext(options);
        var order = await verify.Orders.Include(x => x.Items).Include(x => x.Tickets).Include(x => x.Payment).SingleAsync(x => x.Id == orderId);
        if (order.Status == "Paid")
        {
            Assert.Single(order.Tickets);
            Assert.Single(order.Items);
            Assert.Equal(5, order.Payment!.Amount);
        }
        else
        {
            Assert.Equal("Sent", order.Status);
            Assert.Equal(2, order.Tickets.Count);
            Assert.Equal(2, order.Items.Count);
            Assert.Null(order.Payment);
        }
    }

    [OptInRoundsFact]
    public async Task ConcurrentDuplicateRoundDeductsOnceAndPaysFullBalance()
    {
        var connection = Environment.GetEnvironmentVariable("AAHAR_ROUNDS_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set AAHAR_ROUNDS_TEST_CONNECTION privately for the approved test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Database != "aahar_test") throw new InvalidOperationException("Tests require the dedicated aahar_test database.");
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql(connection).Options;
        var suffix = Guid.NewGuid().ToString("N");
        int menuId, ingredientId;
        await using (var setup = new RestaurantDbContext(options))
        {
            var ingredient = new Ingredient { Name = "round-test-" + suffix, Unit = "kg", Stock = 10 };
            var menu = new MenuItem { Name = "round-test-" + suffix, Category = "test", Price = 5, IsAvailable = true,
                Recipe = [new RecipeIngredient { Ingredient = ingredient, Quantity = 1 }] };
            setup.MenuItems.Add(menu);
            await setup.SaveChangesAsync();
            menuId = menu.Id;
            ingredientId = ingredient.Id;
        }
        static RestaurantService Service(RestaurantDbContext db) => new(db, new PasswordHasher<User>());
        int orderId;
        await using (var db = new RestaurantDbContext(options))
        {
            var service = Service(db);
            var order = await service.CreateOrderAsync(new(null, [new(menuId, 1)]), default);
            orderId = order.Id;
            await service.SendOrderAsync(orderId, "test", default);
        }
        var request = new AddRoundRequest(Guid.NewGuid(), [new(menuId, 2, "extra")]);
        async Task Attempt()
        {
            await using var db = new RestaurantDbContext(options);
            try { await Service(db).AddRoundAsync(orderId, request, "test", default); }
            catch (Exception e) when (DatabaseConflicts.IsConflict(e)) { }
        }
        await Task.WhenAll(Attempt(), Attempt());
        await using (var db = new RestaurantDbContext(options))
        {
            var service = Service(db);
            var repeated = await service.AddRoundAsync(orderId, request, "test", default);
            Assert.Equal(2, repeated.Order.RoundCount);
            Assert.Equal(15, repeated.Order.Total);
            Assert.Equal(7, (await db.Ingredients.SingleAsync(x => x.Id == ingredientId)).Stock);
            Assert.Equal(2, await db.StockMovements.CountAsync(x => x.OrderId == orderId && x.Kind == "Consumption"));
            await Assert.ThrowsAsync<ConflictException>(() => service.AddRoundAsync(orderId,
                new(request.RequestId, [new(menuId, 3, "extra")]), "test", default));
        }
        await using (var db = new RestaurantDbContext(options))
        {
            var service = Service(db);
            var tickets = (await service.KitchenTicketsAsync(default)).Where(x => x.OrderId == orderId).ToList();
            await Assert.ThrowsAsync<DomainException>(() => service.PayAsync(orderId, new("Cash"), default));
            foreach (var ticket in tickets)
            {
                await service.PrepareTicketAsync(ticket.Id, default);
                await service.ReadyTicketAsync(ticket.Id, default);
            }
            var paid = await service.PayAsync(orderId, new("Cash"), default);
            Assert.Equal("Paid", paid.Status);
            Assert.Equal(15, paid.Payment!.Amount);
            Assert.Equal(1, await db.Payments.CountAsync(x => x.OrderId == orderId));
        }
    }
}
