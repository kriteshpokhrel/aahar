using Aahar.Application;
using Microsoft.EntityFrameworkCore;
using Aahar.Domain;

namespace Aahar.Infrastructure;

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

public sealed class ReportRepository(RestaurantDbContext db) : IReportRepository
{
    public Task<PaidSalesTotals?> PaidSalesAsync(BusinessDateRange range, CancellationToken ct) =>
        ReportQueries.PaidSales(db.Payments.AsNoTracking(), range).SingleOrDefaultAsync(ct);
    public Task<List<BestSellingItemDto>> BestSellingAsync(BusinessDateRange range, int limit, CancellationToken ct) =>
        ReportQueries.BestSelling(db.Set<OrderItem>().AsNoTracking(), range, limit).ToListAsync(ct);
}
