using System.Data;
using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aahar.Infrastructure;

public sealed class EfUnitOfWork(RestaurantDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginSerializableAsync(CancellationToken ct) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct));

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

public sealed class GuestRequestRepository(RestaurantDbContext db) : IGuestRequestRepository
{
    public Task<bool> TableExistsAsync(int id, CancellationToken ct) => db.Tables.AnyAsync(x => x.Id == id, ct);
    public Task<bool> TableOccupiedAsync(int id, CancellationToken ct) => db.Orders.AnyAsync(x => x.TableId == id &&
        (x.Status == "Draft" || x.Status == "Sent" || x.Status == "Completed"), ct);

    public async Task<Dictionary<int, MenuItem>> LockMenuAsync(IEnumerable<int> menuIds, CancellationToken ct)
    {
        var ids = menuIds.Distinct().Order().ToArray();
        return (await db.MenuItems.FromSqlInterpolated(
            $"SELECT * FROM \"MenuItems\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR SHARE").ToListAsync(ct))
            .ToDictionary(x => x.Id);
    }

    public void AddRequest(GuestRequest request) => db.GuestRequests.Add(request);
    public Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct) =>
        db.GuestRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct) =>
        db.GuestRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct) =>
        db.GuestRequests.AsNoTracking().Include(x => x.Items)
            .Where(x => (beforeId == null || x.Id < beforeId) && (status == null || x.Status == status))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public void AddOrder(Order order) => db.Orders.Add(order);
}
