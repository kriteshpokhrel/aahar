namespace Aahar.Application;

public record PaidSalesReport(DateOnly FromDate, DateOnly ToDate, string TimeZone, DateTime FromUtc,
    DateTime ToUtcExclusive, long OrdersPaid, decimal Total);
public record BestSellingItemDto(int MenuItemId, string Name, decimal UnitPrice, long Quantity, decimal Revenue);
public record BestSellingReport(DateOnly FromDate, DateOnly ToDate, string TimeZone, DateTime FromUtc,
    DateTime ToUtcExclusive, List<BestSellingItemDto> Items);

public interface IReportService
{
    Task<PaidSalesReport> PaidSalesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);
    Task<BestSellingReport> BestSellingAsync(DateOnly? fromDate, DateOnly? toDate, int limit, CancellationToken ct);
}
