using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public record PaidSalesTotals(long OrdersPaid, decimal Total);

public static class ReportQueries
{
    public static IQueryable<PaidSalesTotals> PaidSales(IQueryable<Payment> payments, BusinessDateRange range) =>
        payments.Where(x => x.Order.Status == "Paid" && x.PaidAt >= range.FromUtc && x.PaidAt < range.ToUtcExclusive)
            .GroupBy(_ => 1).Select(g => new PaidSalesTotals(g.LongCount(), g.Sum(x => x.Amount)));

    public static IQueryable<BestSellingItemDto> BestSelling(IQueryable<OrderItem> items, BusinessDateRange range, int limit) =>
        items.Where(x => x.Order.Status == "Paid" && x.Order.Payment != null &&
            x.Order.Payment.PaidAt >= range.FromUtc && x.Order.Payment.PaidAt < range.ToUtcExclusive)
            .GroupBy(x => new { x.MenuItemId, x.Name, x.UnitPrice })
            .Select(g => new { g.Key.MenuItemId, g.Key.Name, g.Key.UnitPrice,
                Quantity = g.Sum(x => (long)x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Revenue)
            .ThenBy(x => x.MenuItemId).ThenBy(x => x.Name).ThenBy(x => x.UnitPrice).Take(limit)
            .Select(x => new BestSellingItemDto(x.MenuItemId, x.Name, x.UnitPrice, x.Quantity, x.Revenue));
}

public sealed class ReportService(RestaurantDbContext db, TimeZoneInfo businessTimeZone) : IReportService
{
    public async Task<PaidSalesReport> PaidSalesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(fromDate, toDate, businessTimeZone);
        var totals = await ReportQueries.PaidSales(db.Payments.AsNoTracking(), range).SingleOrDefaultAsync(ct);
        return new PaidSalesReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive,
            totals?.OrdersPaid ?? 0, totals?.Total ?? 0m);
    }

    public async Task<BestSellingReport> BestSellingAsync(DateOnly? fromDate, DateOnly? toDate, int limit, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(fromDate, toDate, businessTimeZone);
        if (limit is < 1 or > 100) throw new DomainException("Limit must be between 1 and 100.");
        var items = await ReportQueries.BestSelling(db.Set<OrderItem>().AsNoTracking(), range, limit).ToListAsync(ct);
        return new BestSellingReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive, items);
    }
}
