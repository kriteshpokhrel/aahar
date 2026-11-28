namespace Aahar.Application;

public interface IUnitOfWork
{
    Task<ITransaction> BeginSerializableAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
