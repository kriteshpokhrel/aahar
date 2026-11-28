using Aahar.Domain;

namespace Aahar.Application;

public interface IGuestRequestRepository
{
    Task<bool> TableExistsAsync(int id, CancellationToken ct);
    Task<bool> TableOccupiedAsync(int id, CancellationToken ct);
    Task<Dictionary<int, MenuItem>> LockMenuAsync(IEnumerable<int> menuIds, CancellationToken ct);
    void AddRequest(GuestRequest request);
    Task<GuestRequest?> FindStatusAsync(int id, CancellationToken ct);
    Task<GuestRequest?> FindForDecisionAsync(int id, CancellationToken ct);
    Task<List<GuestRequest>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct);
    void AddOrder(Order order);
}
