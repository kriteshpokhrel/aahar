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

public sealed class StockMovement
{
    public long Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string Kind { get; set; } = "";
    public decimal QuantityChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Actor { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
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
    public int? ConsumptionMovementCount { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationStockDisposition { get; set; }
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

    public static List<StockMovement> DeductStock(Order order, IEnumerable<RecipeIngredient> recipes, string actor)
    {
        var required = recipes.GroupBy(x => x.IngredientId).Select(g => new {
            Ingredient = g.First().Ingredient,
            Amount = g.Sum(x => x.Quantity * order.Items.Single(i => i.MenuItemId == x.MenuItemId).Quantity)
        }).ToList();
        foreach (var row in required) RequireStock(row.Ingredient.Stock, row.Amount, row.Ingredient.Name);
        var movements = new List<StockMovement>();
        foreach (var row in required)
        {
            row.Ingredient.Stock -= row.Amount;
            movements.Add(new StockMovement { Ingredient = row.Ingredient, IngredientId = row.Ingredient.Id,
                OrderId = order.Id, Kind = "Consumption", QuantityChange = -row.Amount, BalanceAfter = row.Ingredient.Stock, Actor = actor });
        }
        order.ConsumptionMovementCount = movements.Count;
        return movements;
    }

    public static List<StockMovement> Cancel(Order order, IReadOnlyList<StockMovement> consumed, string reason, string disposition, string actor, string role)
    {
        if (order.Status is not ("Draft" or "Sent" or "Completed"))
            throw new DomainException("Only unpaid active orders can be cancelled.");
        if (role is not ("Admin" or "Manager" or "Staff"))
            throw new ForbiddenException();
        if (order.Status != "Draft" && role is not ("Admin" or "Manager"))
            throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 300)
            throw new DomainException("Cancellation reason is required and must be at most 300 characters.");
        if ((order.Status == "Draft" && disposition != "None") ||
            (order.Status != "Draft" && disposition is not ("Return" or "Waste")))
            throw new DomainException("Draft cancellations require None; submitted cancellations require Return or Waste.");
        if (order.Status != "Draft" && (order.ConsumptionMovementCount is null || order.ConsumptionMovementCount != consumed.Count ||
            consumed.Any(x => x.Kind != "Consumption" || x.OrderId != order.Id || x.QuantityChange >= 0) ||
            consumed.Select(x => x.IngredientId).Distinct().Count() != consumed.Count))
            throw new DomainException("Stock deductions cannot be reconstructed; this order cannot be cancelled automatically.");
        if (order.Status == "Draft" && consumed.Count != 0)
            throw new DomainException("Draft order has unexpected stock movements.");

        var toReturn = disposition == "Return" ? consumed : [];
        foreach (var movement in toReturn)
            if (movement.Ingredient.Stock - movement.QuantityChange > 99999999999.999m)
                throw new DomainException($"Stock exceeds the maximum for {movement.Ingredient.Name}.");
        var reversals = new List<StockMovement>();
        foreach (var movement in toReturn)
        {
            var ingredient = movement.Ingredient;
            ingredient.Stock -= movement.QuantityChange;
            reversals.Add(new StockMovement { Ingredient = ingredient, IngredientId = ingredient.Id, OrderId = order.Id,
                Kind = "Cancellation", QuantityChange = -movement.QuantityChange, BalanceAfter = ingredient.Stock, Actor = actor });
        }
        order.Status = "Cancelled";
        order.CancellationReason = reason.Trim();
        order.CancellationStockDisposition = disposition;
        order.CancelledBy = actor;
        order.CancelledAt = DateTime.UtcNow;
        return reversals;
    }
}

public sealed class DomainException(string message) : Exception(message);
public sealed class ForbiddenException : Exception;
