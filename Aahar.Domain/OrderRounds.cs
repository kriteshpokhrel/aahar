namespace Aahar.Domain;

public static class OrderRounds
{
    public const int MaxOrderLines = 1000;

    public static bool Matches(KitchenTicket ticket, IReadOnlyList<DraftLine> lines) =>
        ticket.Items.Count == lines.Count &&
        ticket.Items.OrderBy(x => x.MenuItemId).Select(x => (x.MenuItemId, x.Quantity, x.Notes))
            .SequenceEqual(lines.OrderBy(x => x.MenuItemId).Select(x => (x.MenuItemId, x.Quantity, x.Notes)));

    public static (List<OrderItem> Items, KitchenTicket Ticket) Create(Order order, Guid requestId,
        IReadOnlyList<DraftLine> lines, IReadOnlyDictionary<int, MenuItem> menu)
    {
        if (requestId == Guid.Empty) throw new DomainException("A nonempty request ID is required.");
        if (order.Status is not ("Sent" or "Completed")) throw new DomainException("Only submitted unpaid orders can receive a new round.");
        if (lines.Count is < 1 or > 100 || order.Items.Count + lines.Count > MaxOrderLines ||
            lines.Any(x => x is null || x.MenuItemId <= 0 || x.Quantity is < 1 or > 1000 || (x.Notes?.Length ?? 0) > 300) ||
            lines.Select(x => x.MenuItemId).Distinct().Count() != lines.Count)
            throw new DomainException("Invalid round items or order line limit exceeded.");
        var items = new List<OrderItem>();
        decimal total = order.Items.Sum(x => x.UnitPrice * x.Quantity);
        foreach (var line in lines)
        {
            if (!menu.TryGetValue(line.MenuItemId, out var item) || !item.IsAvailable)
                throw new DomainException("A menu item is missing or unavailable.");
            total += item.Price * line.Quantity;
            items.Add(new OrderItem { MenuItemId = item.Id, Name = item.Name, Quantity = line.Quantity, UnitPrice = item.Price, Notes = line.Notes });
        }
        if (total > 999999999999.99m) throw new DomainException("Order total exceeds the maximum.");
        var ticket = new KitchenTicket { OrderId = order.Id, RequestId = requestId, Items = items.Select(x =>
            new KitchenTicketItem { MenuItemId = x.MenuItemId, Name = x.Name, Quantity = x.Quantity, UnitPrice = x.UnitPrice, Notes = x.Notes }).ToList() };
        return (items, ticket);
    }
}
