using Aahar.Domain;

namespace Aahar.Application;

public interface IOrderRepository
{
    Task<List<TableDto>> TablesAsync(CancellationToken ct);
    Task<bool> TableExistsAsync(int id, CancellationToken ct);
    Task<bool> TableOccupiedAsync(int id, CancellationToken ct);
    void AddTable(DiningTable table);
    Task<List<OrderDto>> ListAsync(CancellationToken ct);
    Task<Order?> FindAsync(int id, CancellationToken ct);
    void Add(Order order);
    void RemoveItems(IEnumerable<OrderItem> items);
    Task<SalesDto> SalesAsync(CancellationToken ct);
    Task<List<KitchenTicket>> TicketsAsync(CancellationToken ct);
    Task<KitchenTicket?> FindTicketAsync(int id, CancellationToken ct);
}
