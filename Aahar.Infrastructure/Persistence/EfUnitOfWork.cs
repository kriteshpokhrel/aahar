using System.Data;
using Aahar.Application;
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
