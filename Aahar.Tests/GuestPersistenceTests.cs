using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;

namespace Aahar.Tests;

public class GuestPersistenceTests
{
    [Fact]
    public async Task SubmissionSavesAndCommitsWithinTheUnitOfWork()
    {
        var repository = new FakeRepository();
        var unit = new FakeUnitOfWork();
        var service = new GuestRequestService(repository, unit);

        var result = await service.SubmitAsync(new SubmitGuestRequest(null, [new(1, 2, 100m)]), default);

        Assert.NotNull(repository.Request);
        Assert.Equal(2, Assert.Single(repository.Request.Items).Quantity);
        Assert.True(GuestAccessToken.Matches(result.AccessToken, repository.Request.AccessTokenHash));
        Assert.Equal(new[] { "begin", "save", "commit", "dispose" }, unit.Events);
    }

    [Fact]
    public async Task ChangedQuoteDisposesTransactionWithoutSavingOrCommitting()
    {
        var repository = new FakeRepository();
        var unit = new FakeUnitOfWork();
        var service = new GuestRequestService(repository, unit);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.SubmitAsync(new SubmitGuestRequest(null, [new(1, 1, 99m)]), default));

        Assert.Null(repository.Request);
        Assert.Equal(new[] { "begin", "dispose" }, unit.Events);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork, ITransaction
    {
        public List<string> Events { get; } = [];
        public Task<ITransaction> BeginSerializableAsync(CancellationToken ct)
        {
            Events.Add("begin");
            return Task.FromResult<ITransaction>(this);
        }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            Events.Add("save");
            return Task.FromResult(1);
        }
        public Task CommitAsync(CancellationToken ct)
        {
            Events.Add("commit");
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRepository : IGuestRequestRepository
    {
        public GuestRequest? Request { get; private set; }
        public Task<Dictionary<int, MenuItem>> LockMenuAsync(IEnumerable<int> menuIds, CancellationToken ct) =>
            Task.FromResult(new Dictionary<int, MenuItem>
            {
                [1] = new() { Id = 1, Name = "Example dish", Price = 100m, IsAvailable = true }
            });
        public void AddRequest(GuestRequest request) => Request = request;
        public Task<bool> TableExistsAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TableOccupiedAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public void AddOrder(Order order) => throw new NotSupportedException();
    }
}
