namespace Aahar.Domain;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Staff";
}

public sealed class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public List<RecipeIngredient> Recipe { get; set; } = [];
}

public sealed class DiningTable
{
    public int Id { get; set; }
    public int Number { get; set; }
    public int Capacity { get; set; }
}

public sealed class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Stock { get; set; }
}

public sealed class RecipeIngredient
{
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public decimal Quantity { get; set; }
}

public sealed class Order
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public DiningTable Table { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

public static class OrderRules
{
    public static void RequireTransition(string current, string expected)
    {
        if (current != expected) throw new DomainException($"Order must be {expected}.");
    }

    public static void RequireStock(decimal available, decimal required, string name)
    {
        if (available < required) throw new DomainException($"Insufficient stock for {name}.");
    }

    public static void DeductStock(Order order, IEnumerable<RecipeIngredient> recipes)
    {
        var required = recipes.GroupBy(x => x.IngredientId).Select(g => new {
            Ingredient = g.First().Ingredient,
            Amount = g.Sum(x => x.Quantity * order.Items.Single(i => i.MenuItemId == x.MenuItemId).Quantity)
        }).ToList();
        foreach (var row in required) RequireStock(row.Ingredient.Stock, row.Amount, row.Ingredient.Name);
        foreach (var row in required) row.Ingredient.Stock -= row.Amount;
    }
}

public sealed class DomainException(string message) : Exception(message);
