using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Tests;

public class ReportTests
{
    [Fact]
    public void BusinessDayUsesExplicitNepalOffsetAndExclusiveEnd()
    {
        var range = BusinessDateRange.Create(new(2026, 11, 26), new(2026, 11, 26), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu"));
        Assert.Equal(new DateTime(2026, 11, 25, 18, 15, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 26, 18, 15, 0, DateTimeKind.Utc), range.ToUtcExclusive);
        Assert.Equal("Asia/Kathmandu", range.TimeZone);
    }

    [Fact]
    public void BusinessDaysHonorDaylightSavingTransitions()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var spring = BusinessDateRange.Create(new(2024, 3, 10), new(2024, 3, 10), zone);
        var fall = BusinessDateRange.Create(new(2024, 11, 3), new(2024, 11, 3), zone);
        Assert.Equal(TimeSpan.FromHours(23), spring.ToUtcExclusive - spring.FromUtc);
        Assert.Equal(TimeSpan.FromHours(25), fall.ToUtcExclusive - fall.FromUtc);
        var midnightGap = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(new(2019, 9, 8), new(2019, 9, 8), midnightGap));
        var midnightOverlap = TimeZoneInfo.FindSystemTimeZoneById("America/Havana");
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(new(2020, 11, 1), new(2020, 11, 1), midnightOverlap));
    }

    [Fact]
    public void DatesMustBeValidOrderedAndAtMost366Days()
    {
        var zone = TimeZoneInfo.Utc;
        Assert.Throws<DomainException>(() => BusinessDateRange.Parse("11/26/2026"));
        Assert.Throws<DomainException>(() => BusinessDateRange.Parse("2026-02-30"));
        Assert.Null(BusinessDateRange.Parse(null));
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(null, new(2026, 1, 1), zone));
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(new(2026, 2, 1), new(2026, 1, 1), zone));
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(new(2024, 1, 1), new(2025, 1, 1), zone));
        Assert.Throws<DomainException>(() => BusinessDateRange.Create(new(2026, 1, 1), DateOnly.MaxValue, zone));
        BusinessDateRange.Create(new(2024, 1, 1), new(2024, 12, 31), zone);
    }

    [Fact]
    public void ReportsFilterPaidAtBoundariesAndRetainHistoricalSnapshots()
    {
        var range = BusinessDateRange.Create(new(2026, 1, 1), new(2026, 1, 1), TimeZoneInfo.Utc);
        var inside = new Order { Status = "Paid" };
        var payment = new Payment { Order = inside, PaidAt = range.FromUtc, Amount = 50 };
        inside.Payment = payment;
        var secondOrder = new Order { Status = "Paid" };
        var secondPayment = new Payment { Order = secondOrder, PaidAt = range.ToUtcExclusive.AddTicks(-1), Amount = 10 };
        secondOrder.Payment = secondPayment;
        var paidBefore = new Order { Status = "Paid" };
        var before = new Payment { Order = paidBefore, PaidAt = range.FromUtc.AddTicks(-1), Amount = 999 };
        paidBefore.Payment = before;
        var paidAfter = new Order { Status = "Paid" };
        var after = new Payment { Order = paidAfter, PaidAt = range.ToUtcExclusive, Amount = 999 };
        paidAfter.Payment = after;
        var cancelled = new Order { Status = "Cancelled" };
        var ignored = new Payment { Order = cancelled, PaidAt = range.FromUtc, Amount = 999 };
        cancelled.Payment = ignored;
        var totals = Assert.Single(ReportQueries.PaidSales(new[] { payment, secondPayment, before, after, ignored }.AsQueryable(), range));
        Assert.Equal(new PaidSalesTotals(2, 60m), totals);
        var items = new[] {
            new OrderItem { Order = inside, MenuItemId = 1, Name = "Old name", UnitPrice = 10, Quantity = 2 },
            new OrderItem { Order = secondOrder, MenuItemId = 1, Name = "Old name", UnitPrice = 10, Quantity = 1 },
            new OrderItem { Order = inside, MenuItemId = 1, Name = "New name", UnitPrice = 30, Quantity = 1 },
            new OrderItem { Order = paidBefore, MenuItemId = 1, Name = "New name", UnitPrice = 30, Quantity = 9 },
            new OrderItem { Order = paidAfter, MenuItemId = 1, Name = "New name", UnitPrice = 30, Quantity = 9 },
            new OrderItem { Order = cancelled, MenuItemId = 1, Name = "New name", UnitPrice = 30, Quantity = 9 },
            new OrderItem { Order = new Order { Status = "Sent" }, MenuItemId = 2, Name = "Unpaid", UnitPrice = 50, Quantity = 9 }
        };
        var best = ReportQueries.BestSelling(items.AsQueryable(), range, 20).ToList();
        Assert.Equal(2, best.Count);
        Assert.Equal(new BestSellingItemDto(1, "Old name", 10m, 3, 30m), best[0]);
        Assert.Equal(new BestSellingItemDto(1, "New name", 30m, 1, 30m), best[1]);
        Assert.Single(ReportQueries.BestSelling(items.AsQueryable(), range, 1));
        Assert.Empty(ReportQueries.PaidSales(Array.Empty<Payment>().AsQueryable(), range));
    }

    [Fact]
    public void ReportQueriesTranslateToPostgresSqlOffline()
    {
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var range = BusinessDateRange.Create(new(2026, 1, 1), new(2026, 1, 1), TimeZoneInfo.Utc);
        var salesSql = ReportQueries.PaidSales(db.Payments, range).ToQueryString();
        var itemSql = ReportQueries.BestSelling(db.Set<OrderItem>(), range, 20).ToQueryString();
        Assert.Contains("\"PaidAt\"", salesSql);
        Assert.Contains("GROUP BY", itemSql);
        Assert.Contains("\"UnitPrice\"", itemSql);
        Assert.DoesNotContain("JOIN \"MenuItems\"", itemSql);
    }

    [Fact]
    public async Task ReportsRequireManagerAndStrictDates()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var uri = "/api/reports/sales?fromDate=2026-11-26&toDate=2026-11-26";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(uri)).StatusCode);
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/best-selling?fromDate=2026-11-26&toDate=2026-11-26")).StatusCode);
    }

    [Fact]
    public async Task ManagerReceivesReportAndInvalidDateReturnsBadRequest()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("manager", "password"))).StatusCode);
        var response = await client.GetAsync("/api/reports/sales?fromDate=2026-11-26&toDate=2026-11-26");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<PaidSalesReport>())!;
        Assert.Equal("Asia/Kathmandu", report.TimeZone);
        Assert.Equal(1, report.OrdersPaid);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/sales?fromDate=11%2F26%2F2026&toDate=2026-11-26")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/best-selling?fromDate=2026-11-26&toDate=2026-11-26&limit=0")).StatusCode);
    }

    private record Csrf(string RequestToken);
}

public sealed class FakeReportService : IReportService
{
    public Task<PaidSalesReport> PaidSalesAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(from, to, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu"));
        return Task.FromResult(new PaidSalesReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive, 1, 50));
    }
    public Task<BestSellingReport> BestSellingAsync(DateOnly? from, DateOnly? to, int limit, CancellationToken ct)
    {
        var range = BusinessDateRange.Create(from, to, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu"));
        if (limit is < 1 or > 100) throw new DomainException("Limit must be between 1 and 100.");
        return Task.FromResult(new BestSellingReport(range.FromDate, range.ToDate, range.TimeZone, range.FromUtc, range.ToUtcExclusive, []));
    }
}
