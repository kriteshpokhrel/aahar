using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class OrderRepository(RestaurantDbContext db) : IOrderRepository
{
    public Task<List<TableDto>> TablesAsync(CancellationToken ct) =>
        db.Tables.AsNoTracking().OrderBy(x => x.Number).Select(x => new TableDto(x.Id, x.Number, x.Capacity,
            db.Orders.Any(o => o.TableId == x.Id && (o.Status == "Draft" || o.Status == "Sent" || o.Status == "Completed")))).ToListAsync(ct);
    public Task<bool> TableExistsAsync(int id, CancellationToken ct) => db.Tables.AnyAsync(x => x.Id == id, ct);
    public Task<bool> TableOccupiedAsync(int id, CancellationToken ct) =>
        db.Orders.AnyAsync(x => x.TableId == id && (x.Status == "Draft" || x.Status == "Sent" || x.Status == "Completed"), ct);
    public void AddTable(DiningTable table) => db.Tables.Add(table);
    public Task<List<OrderDto>> ListAsync(CancellationToken ct) =>
        OrderQueries.List(db.Orders.AsNoTracking().AsSingleQuery()).ToListAsync(ct);
    public Task<Order?> FindAsync(int id, CancellationToken ct) =>
        db.Orders.AsSplitQuery().Include(x => x.Items).Include(x => x.Payment)
            .Include(x => x.Tickets).ThenInclude(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(Order order) => db.Orders.Add(order);
    public void RemoveItems(IEnumerable<OrderItem> items) => db.Set<OrderItem>().RemoveRange(items);
    public async Task<SalesDto> SalesAsync(CancellationToken ct) =>
        new(await db.Payments.CountAsync(ct), await db.Payments.SumAsync(x => x.Amount, ct));
    public Task<List<KitchenTicket>> TicketsAsync(CancellationToken ct) =>
        db.KitchenTickets.AsNoTracking().Include(x => x.Order).Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public Task<KitchenTicket?> FindTicketAsync(int id, CancellationToken ct) =>
        db.KitchenTickets.Include(x => x.Order).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
}

public static class OrderQueries
{
    public static IQueryable<OrderDto> List(IQueryable<Order> orders) =>
        orders.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => new OrderDto(
            x.Id, x.TableId, x.Status, x.CreatedAt,
            x.Items.OrderBy(item => item.Id)
                .Select(item => new OrderItemDto(item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.Notes)).ToList(),
            x.Items.Sum(item => item.UnitPrice * item.Quantity),
            x.Payment == null ? null : new PaymentDto(x.Payment.Id, x.Payment.Method, x.Payment.Amount, x.Payment.PaidAt),
            x.CancelledAt, x.CancelledBy, x.CancellationReason, x.CancellationStockDisposition, x.Tickets.Count));
}
