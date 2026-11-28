using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class GuestRequestRepository(RestaurantDbContext db, IMenuRepository menu, IOrderRepository orders) : IGuestRequestRepository
{
    public Task<bool> TableExistsAsync(int id, CancellationToken ct) => orders.TableExistsAsync(id, ct);
    public Task<bool> TableOccupiedAsync(int id, CancellationToken ct) => orders.TableOccupiedAsync(id, ct);

    public Task<Dictionary<int, MenuItem>> LockMenuAsync(IEnumerable<int> menuIds, CancellationToken ct) =>
        menu.LockForShareAsync(menuIds, ct);

    public void AddRequest(GuestRequest request) => db.GuestRequests.Add(request);
    public Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct) =>
        db.GuestRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct) =>
        db.GuestRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct) =>
        db.GuestRequests.AsNoTracking().Include(x => x.Items)
            .Where(x => (beforeId == null || x.Id < beforeId) && (status == null || x.Status == status))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public void AddOrder(Order order) => orders.Add(order);
}
