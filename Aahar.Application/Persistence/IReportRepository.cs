using Aahar.Domain;

namespace Aahar.Application;

public record PaidSalesTotals(long OrdersPaid, decimal Total);

public interface IReportRepository
{
    Task<PaidSalesTotals?> PaidSalesAsync(BusinessDateRange range, CancellationToken ct);
    Task<List<BestSellingItemDto>> BestSellingAsync(BusinessDateRange range, int limit, CancellationToken ct);
}
