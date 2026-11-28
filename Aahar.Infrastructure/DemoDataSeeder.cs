using Aahar.Application;
using Aahar.Domain;

namespace Aahar.Infrastructure;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IInstallationRepository installation, IMenuRepository menu,
        IOrderRepository orders, IInventoryRepository inventory, IUnitOfWork unitOfWork, CancellationToken ct = default)
    {
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        await installation.LockDemoSeedAsync(ct);
        if (await installation.RestaurantDataExistsAsync(ct))
            throw new InvalidOperationException("Demo seed refused: restaurant data already exists.");

        var rice = new Ingredient { Name = "Rice", Unit = "kg", Stock = 20m };
        var vegetables = new Ingredient { Name = "Vegetables", Unit = "kg", Stock = 10m };
        var tea = new Ingredient { Name = "Tea leaves", Unit = "kg", Stock = 2m };
        var ingredients = new[] { (rice, 3m), (vegetables, 2m), (tea, 0.5m) };
        foreach (var (ingredient, threshold) in ingredients)
        {
            inventory.AddIngredient(ingredient);
            inventory.AddThresholdChange(InventoryRules.ChangeThreshold(
                ingredient, threshold, "Demo opening threshold", "demo-seed", initial: true));
            inventory.AddMovement(new StockMovement
            {
                Ingredient = ingredient,
                Kind = "Opening",
                QuantityChange = ingredient.Stock,
                BalanceAfter = ingredient.Stock,
                Actor = "demo-seed",
                Reason = "Demo opening balance"
            });
        }

        var items = new[]
        {
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
            }
        };
        foreach (var item in items) menu.Add(item);
        foreach (var number in Enumerable.Range(1, 6))
            orders.AddTable(new DiningTable { Number = number, Capacity = number is 1 or 5 ? 2 : 4 });
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
