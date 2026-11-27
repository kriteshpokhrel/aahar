namespace Aahar.Domain;

public sealed class GuestRequest
{
    public int Id { get; set; }
    public int? TableId { get; set; }
    public DiningTable? Table { get; set; }
    public string Status { get; set; } = "Pending";
    public byte[] AccessTokenHash { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? RejectionReason { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public long Revision { get; set; }
    public List<GuestRequestItem> Items { get; set; } = [];
}

public sealed class GuestRequestItem
{
    public int Id { get; set; }
    public int GuestRequestId { get; set; }
    public GuestRequest Request { get; set; } = null!;
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal QuotedUnitPrice { get; set; }
    public string? Notes { get; set; }
}

public record GuestQuoteLine(int MenuItemId, int Quantity, decimal? QuotedUnitPrice, string? Notes);

public static class GuestRequestRules
{
    public static List<GuestRequestItem> ValidateQuote(IReadOnlyList<GuestQuoteLine> lines, IReadOnlyDictionary<int, MenuItem> menu)
    {
        if (lines.Count is < 1 or > 100 || lines.Any(x => x is null || x.MenuItemId <= 0 ||
            x.Quantity is < 1 or > 1000 || x.QuotedUnitPrice is null or < 0 or > 9999999999.99m ||
            decimal.Round(x.QuotedUnitPrice.Value, 2) != x.QuotedUnitPrice.Value || (x.Notes?.Length ?? 0) > 300) ||
            lines.Select(x => x.MenuItemId).Distinct().Count() != lines.Count)
            throw new DomainException("Invalid guest request items.");

        var result = new List<GuestRequestItem>();
        decimal total = 0;
        foreach (var line in lines)
        {
            if (!menu.TryGetValue(line.MenuItemId, out var item) || !item.IsAvailable || item.Price != line.QuotedUnitPrice)
                throw new ConflictException("The menu has changed. Refresh prices and availability.");
            total += item.Price * line.Quantity;
            result.Add(new GuestRequestItem { MenuItemId = item.Id, Name = item.Name, Quantity = line.Quantity,
                QuotedUnitPrice = item.Price, Notes = line.Notes });
        }
        if (total > 999999999999.99m) throw new DomainException("Request total exceeds the maximum.");
        return result;
    }

    public static void Accept(GuestRequest request, Order order, string actor)
    {
        Decide(request, actor);
        request.Status = "Accepted";
        request.Order = order;
    }

    public static void Reject(GuestRequest request, string reason, string actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 300)
            throw new DomainException("Rejection reason is required and must be at most 300 characters.");
        Decide(request, actor);
        request.Status = "Rejected";
        request.RejectionReason = reason.Trim();
    }

    private static void Decide(GuestRequest request, string actor)
    {
        if (request.Status != "Pending") throw new ConflictException("This request has already been decided.");
        if (request.Revision == long.MaxValue) throw new DomainException("Request revision exceeds the maximum.");
        request.Revision++;
        request.DecidedBy = actor;
        request.DecidedAt = DateTime.UtcNow;
    }
}

public sealed class ConflictException(string message) : Exception(message);
