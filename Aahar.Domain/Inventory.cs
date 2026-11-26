namespace Aahar.Domain;

public sealed class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public long Revision { get; set; }
}

public sealed class SupplierChange
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public string Actor { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public sealed class IngredientThresholdChange
{
    public long Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public decimal? PreviousThreshold { get; set; }
    public decimal NewThreshold { get; set; }
    public string Actor { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public sealed class PurchaseReceipt
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string SupplierName { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public List<PurchaseReceiptLine> Lines { get; set; } = [];
}

public sealed class PurchaseReceiptLine
{
    public int Id { get; set; }
    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public string IngredientName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public record PurchaseLine(int IngredientId, decimal Quantity, string Unit, decimal? UnitCost);

public static class InventoryRules
{
    public const decimal MaxQuantity = 99999999999.999m;
    public const decimal MaxMoney = 9999999999.99m;
    public const decimal MaxTotal = 999999999999.99m;

    public static bool ValidQuantity(decimal quantity) =>
        quantity >= 0 && quantity <= MaxQuantity && decimal.Round(quantity, 3) == quantity;

    public static string RequiredText(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new DomainException($"{field} is required and must be at most {max} characters.");
        return value.Trim();
    }

    public static SupplierChange ChangeSupplier(Supplier supplier, string name, string? contact, string reason, string actor)
    {
        var validatedName = RequiredText(name, 100, "Supplier name");
        var validatedReason = RequiredText(reason, 300, "Reason");
        if (contact is not null && contact.Trim().Length > 200) throw new DomainException("Contact is too long.");
        if (supplier.Revision == long.MaxValue) throw new DomainException("Supplier revision exceeds the maximum.");
        supplier.Name = validatedName;
        supplier.Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        supplier.Revision++;
        return new SupplierChange { Supplier = supplier, Name = supplier.Name, Contact = supplier.Contact, Actor = actor, Reason = validatedReason };
    }

    public static IngredientThresholdChange ChangeThreshold(Ingredient ingredient, decimal threshold, string reason, string actor, bool initial = false)
    {
        if (!ValidQuantity(threshold)) throw new DomainException("Invalid low-stock threshold.");
        var validatedReason = RequiredText(reason, 300, "Reason");
        if (!initial && ingredient.LowStockThreshold == threshold) throw new DomainException("Threshold is unchanged.");
        var previous = initial ? (decimal?)null : ingredient.LowStockThreshold;
        ingredient.LowStockThreshold = threshold;
        return new IngredientThresholdChange { Ingredient = ingredient, PreviousThreshold = previous, NewThreshold = threshold, Actor = actor, Reason = validatedReason };
    }

    public static StockMovement Waste(Ingredient ingredient, decimal quantity, string unit, string reason, string actor)
    {
        if (!ValidQuantity(quantity) || quantity <= 0) throw new DomainException("Invalid waste quantity.");
        if (unit != ingredient.Unit) throw new DomainException("Unit does not match the ingredient.");
        var validatedReason = RequiredText(reason, 300, "Reason");
        OrderRules.RequireStock(ingredient.Stock, quantity, ingredient.Name);
        ingredient.Stock -= quantity;
        return new StockMovement { Ingredient = ingredient, IngredientId = ingredient.Id, Kind = "Waste",
            QuantityChange = -quantity, BalanceAfter = ingredient.Stock, Actor = actor, Reason = validatedReason };
    }

    public static (PurchaseReceipt Receipt, List<StockMovement> Movements) Receive(
        Supplier supplier, string reference, string reason, IReadOnlyList<PurchaseLine> lines,
        IReadOnlyDictionary<int, Ingredient> ingredients, string actor)
    {
        var validatedReference = RequiredText(reference, 100, "Reference");
        var validatedReason = RequiredText(reason, 300, "Reason");
        if (lines.Count is < 1 or > 100 || lines.Any(x => x is null || x.IngredientId <= 0 ||
            !ValidQuantity(x.Quantity) || x.Quantity <= 0 || x.UnitCost is null or < 0 or > MaxMoney ||
            decimal.Round(x.UnitCost.Value, 2) != x.UnitCost.Value) ||
            lines.Select(x => x.IngredientId).Distinct().Count() != lines.Count)
            throw new DomainException("Invalid purchase receipt lines.");
        decimal total = 0;
        foreach (var line in lines)
        {
            if (!ingredients.TryGetValue(line.IngredientId, out var ingredient))
                throw new DomainException("Ingredient not found.");
            if (line.Unit != ingredient.Unit) throw new DomainException($"Unit does not match {ingredient.Name}.");
            if (!ValidQuantity(ingredient.Stock + line.Quantity)) throw new DomainException($"Stock exceeds the maximum for {ingredient.Name}.");
            total += decimal.Round(line.Quantity * line.UnitCost!.Value, 2, MidpointRounding.AwayFromZero);
        }
        if (total > MaxTotal) throw new DomainException("Purchase total exceeds the maximum.");
        var receipt = new PurchaseReceipt { SupplierId = supplier.Id, SupplierName = supplier.Name,
            Reference = validatedReference, Reason = validatedReason, Actor = actor, Total = total };
        var movements = new List<StockMovement>();
        foreach (var line in lines)
        {
            var ingredient = ingredients[line.IngredientId];
            var receiptLine = new PurchaseReceiptLine { IngredientId = ingredient.Id, IngredientName = ingredient.Name,
                Unit = ingredient.Unit, Quantity = line.Quantity, UnitCost = line.UnitCost!.Value,
                LineTotal = decimal.Round(line.Quantity * line.UnitCost.Value, 2, MidpointRounding.AwayFromZero) };
            receipt.Lines.Add(receiptLine);
            ingredient.Stock += line.Quantity;
            movements.Add(new StockMovement { Ingredient = ingredient, IngredientId = ingredient.Id, PurchaseReceiptLine = receiptLine,
                Kind = "Purchase", QuantityChange = line.Quantity, BalanceAfter = ingredient.Stock, Actor = actor, Reason = validatedReason });
        }
        return (receipt, movements);
    }
}
