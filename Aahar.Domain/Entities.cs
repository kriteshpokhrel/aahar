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
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock => Stock <= LowStockThreshold;
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
    public string? Reason { get; set; }
    public int? PurchaseReceiptLineId { get; set; }
    public PurchaseReceiptLine? PurchaseReceiptLine { get; set; }
    public int? KitchenTicketId { get; set; }
    public KitchenTicket? KitchenTicket { get; set; }
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
    public int? TableId { get; set; }
    public DiningTable? Table { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long Revision { get; set; }
    public int? ConsumptionMovementCount { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationStockDisposition { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
    public List<KitchenTicket> Tickets { get; set; } = [];
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
    public string? Notes { get; set; }
}

public sealed class KitchenTicket
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string Status { get; set; } = "Queued";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsReconstructed { get; set; }
    public Guid? RequestId { get; set; }
    public List<KitchenTicketItem> Items { get; set; } = [];
}

public sealed class KitchenTicketItem
{
    public int Id { get; set; }
    public int KitchenTicketId { get; set; }
    public KitchenTicket Ticket { get; set; } = null!;
    public int MenuItemId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public record DraftLine(int MenuItemId, int Quantity, string? Notes);

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
    public static void SetDraftItems(Order order, IReadOnlyList<DraftLine> lines, IReadOnlyDictionary<int, MenuItem> menu)
    {
        RequireTransition(order.Status, "Draft");
        if (lines.Count is < 1 or > 100 || lines.Any(x => x is null || x.MenuItemId <= 0 ||
            x.Quantity is < 1 or > 1000 || (x.Notes?.Length ?? 0) > 300) ||
            lines.Select(x => x.MenuItemId).Distinct().Count() != lines.Count)
            throw new DomainException("Invalid order items.");

        var existing = order.Items.ToDictionary(x => x.MenuItemId);
        decimal total = 0;
        foreach (var line in lines)
        {
            if (!menu.TryGetValue(line.MenuItemId, out var item))
                throw new DomainException("A menu item is missing.");
            existing.TryGetValue(line.MenuItemId, out var previous);
            if (!item.IsAvailable && (previous is null || line.Quantity > previous.Quantity))
                throw new DomainException("A menu item is unavailable.");
            total += (previous?.UnitPrice ?? item.Price) * line.Quantity;
        }
        if (total > 999999999999.99m) throw new DomainException("Order total exceeds the maximum.");
        Touch(order);

        var updated = new List<OrderItem>();
        foreach (var line in lines)
        {
            var item = existing.GetValueOrDefault(line.MenuItemId) ?? new OrderItem
            {
                MenuItemId = line.MenuItemId,
                Name = menu[line.MenuItemId].Name,
                UnitPrice = menu[line.MenuItemId].Price
            };
            item.Quantity = line.Quantity;
            item.Notes = line.Notes;
            updated.Add(item);
        }
        order.Items = updated;
    }

    public static void Touch(Order order)
    {
        if (order.Revision == long.MaxValue) throw new DomainException("Order revision exceeds the maximum.");
        order.Revision++;
    }

    public static void RequireTransition(string current, string expected)
    {
        if (current != expected) throw new DomainException($"Order must be {expected}.");
    }

    public static void RequireStock(decimal available, decimal required, string name)
    {
        if (available < required) throw new DomainException($"Insufficient stock for {name}.");
    }

    public static List<StockMovement> DeductStock(Order order, IEnumerable<RecipeIngredient> recipes, string actor)
        => DeductStock(order, order.Items, recipes, actor, null, initial: true);

    public static List<StockMovement> DeductStock(Order order, IReadOnlyList<OrderItem> items, IEnumerable<RecipeIngredient> recipes,
        string actor, KitchenTicket? ticket, bool initial)
    {
        var required = recipes.GroupBy(x => x.IngredientId).Select(g => new {
            Ingredient = g.First().Ingredient,
            Amount = g.Sum(x => x.Quantity * items.Single(i => i.MenuItemId == x.MenuItemId).Quantity)
        }).ToList();
        foreach (var row in required) RequireStock(row.Ingredient.Stock, row.Amount, row.Ingredient.Name);
        var movements = new List<StockMovement>();
        foreach (var row in required)
        {
            row.Ingredient.Stock -= row.Amount;
            movements.Add(new StockMovement { Ingredient = row.Ingredient, IngredientId = row.Ingredient.Id,
                OrderId = order.Id, KitchenTicket = ticket, Kind = "Consumption", QuantityChange = -row.Amount, BalanceAfter = row.Ingredient.Stock, Actor = actor, Reason = "Order sent to kitchen" });
        }
        if (initial) order.ConsumptionMovementCount = movements.Count;
        else if (order.ConsumptionMovementCount is int count) order.ConsumptionMovementCount = checked(count + movements.Count);
        return movements;
    }

    public static List<StockMovement> Cancel(Order order, IReadOnlyList<StockMovement> consumed, string reason, string disposition, string actor, string role)
    {
        if (order.Status is not ("Draft" or "Sent" or "Completed"))
            throw new DomainException("Only unpaid active orders can be cancelled.");
        if (order.Tickets.Any(x => x.Status == "Cancelled"))
            throw new DomainException("Kitchen ticket is already cancelled.");
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
            consumed.GroupBy(x => new { x.KitchenTicketId, x.IngredientId }).Any(g => g.Count() != 1)))
            throw new DomainException("Stock deductions cannot be reconstructed; this order cannot be cancelled automatically.");
        if (order.Status == "Draft" && consumed.Count != 0)
            throw new DomainException("Draft order has unexpected stock movements.");

        var toReturn = disposition == "Return" ? consumed.GroupBy(x => x.IngredientId)
            .Select(g => new { Ingredient = g.First().Ingredient, Amount = -g.Sum(x => x.QuantityChange) }).ToList() : [];
        foreach (var row in toReturn)
            if (row.Ingredient.Stock + row.Amount > 99999999999.999m)
                throw new DomainException($"Stock exceeds the maximum for {row.Ingredient.Name}.");
        Touch(order);
        var reversals = new List<StockMovement>();
        foreach (var row in toReturn)
        {
            var ingredient = row.Ingredient;
            ingredient.Stock += row.Amount;
            reversals.Add(new StockMovement { Ingredient = ingredient, IngredientId = ingredient.Id, OrderId = order.Id,
                Kind = "Cancellation", QuantityChange = row.Amount, BalanceAfter = ingredient.Stock, Actor = actor, Reason = reason.Trim() });
        }
        order.Status = "Cancelled";
        foreach (var ticket in order.Tickets) ticket.Status = "Cancelled";
        order.CancellationReason = reason.Trim();
        order.CancellationStockDisposition = disposition;
        order.CancelledBy = actor;
        order.CancelledAt = DateTime.UtcNow;
        return reversals;
    }
}

public static class KitchenRules
{
    public static KitchenTicket CreateTicket(Order order)
    {
        OrderRules.RequireTransition(order.Status, "Draft");
        return new KitchenTicket { OrderId = order.Id, Items = order.Items.Select(x => new KitchenTicketItem {
            MenuItemId = x.MenuItemId, Name = x.Name, Quantity = x.Quantity, UnitPrice = x.UnitPrice, Notes = x.Notes
        }).ToList() };
    }

    public static void Transition(KitchenTicket ticket, string from, string to)
    {
        OrderRules.RequireTransition(ticket.Order.Status, "Sent");
        OrderRules.RequireTransition(ticket.Status, from);
        OrderRules.Touch(ticket.Order);
        ticket.Status = to;
    }

    public static void Complete(Order order)
    {
        OrderRules.RequireTransition(order.Status, "Sent");
        RequireReady(order);
        OrderRules.Touch(order);
        order.Status = "Completed";
    }

    public static void RequireReady(Order order)
    {
        if (order.Tickets.Count == 0) throw new DomainException("Kitchen ticket is missing.");
        if (order.Tickets.Any(x => x.Status != "Ready")) throw new DomainException("All kitchen tickets must be Ready.");
    }
}

public sealed class DomainException(string message) : Exception(message);
public sealed class ForbiddenException : Exception;
