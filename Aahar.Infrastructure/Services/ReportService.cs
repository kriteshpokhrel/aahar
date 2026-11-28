using Aahar.Application;
using Aahar.Domain;

namespace Aahar.Infrastructure;

public sealed class ReportService(IReportRepository repository, TimeZoneInfo businessTimeZone) : IReportService
{
    public async Task<PaidSalesReport> PaidSalesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(fromDate, toDate, businessTimeZone);
        var totals = await repository.PaidSalesAsync(range, ct);
        return new PaidSalesReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive,
            totals?.OrdersPaid ?? 0, totals?.Total ?? 0m);
    }

    public async Task<BestSellingReport> BestSellingAsync(DateOnly? fromDate, DateOnly? toDate, int limit, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(fromDate, toDate, businessTimeZone);
        if (limit is < 1 or > 100) throw new DomainException("Limit must be between 1 and 100.");
        var items = await repository.BestSellingAsync(range, limit, ct);
        return new BestSellingReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive, items);
    }
}
