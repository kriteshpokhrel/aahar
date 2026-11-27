using System.Data;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(RestaurantDbContext db, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094799699)", ct);
        if (await db.MenuItems.AnyAsync(ct) || await db.Tables.AnyAsync(ct) ||
            await db.Ingredients.AnyAsync(ct) || await db.Orders.AnyAsync(ct) ||
            await db.GuestRequests.AnyAsync(ct) || await db.Suppliers.AnyAsync(ct))
            throw new InvalidOperationException("Demo seed refused: restaurant data already exists.");

        var rice = new Ingredient { Name = "Rice", Unit = "kg", Stock = 20m };
        var vegetables = new Ingredient { Name = "Vegetables", Unit = "kg", Stock = 10m };
        var tea = new Ingredient { Name = "Tea leaves", Unit = "kg", Stock = 2m };
        var ingredients = new[] { (rice, 3m), (vegetables, 2m), (tea, 0.5m) };
        foreach (var (ingredient, threshold) in ingredients)
        {
            db.Ingredients.Add(ingredient);
            db.IngredientThresholdChanges.Add(InventoryRules.ChangeThreshold(
                ingredient, threshold, "Demo opening threshold", "demo-seed", initial: true));
            db.StockMovements.Add(new StockMovement
            {
                Ingredient = ingredient,
                Kind = "Opening",
                QuantityChange = ingredient.Stock,
                BalanceAfter = ingredient.Stock,
                Actor = "demo-seed",
                Reason = "Demo opening balance"
            });
        }

        db.MenuItems.AddRange(
            new MenuItem
            {
                Name = "Vegetable rice bowl", Category = "Mains", Description = "Rice with seasonal vegetables",
                Price = 320m, Recipe =
                [
                    new RecipeIngredient { Ingredient = rice, Quantity = 0.2m },
                    new RecipeIngredient { Ingredient = vegetables, Quantity = 0.15m }
                ]
            },
            new MenuItem
            {
                Name = "Vegetable soup", Category = "Small plates", Description = "Warm vegetable soup",
                Price = 220m, Recipe = [new RecipeIngredient { Ingredient = vegetables, Quantity = 0.15m }]
            },
            new MenuItem
            {
                Name = "Masala tea", Category = "Drinks", Description = "Tea with warming spices",
                Price = 120m, Recipe = [new RecipeIngredient { Ingredient = tea, Quantity = 0.01m }]
            });
        db.Tables.AddRange(Enumerable.Range(1, 6).Select(number => new DiningTable
        {
            Number = number,
            Capacity = number is 1 or 5 ? 2 : 4
        }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
