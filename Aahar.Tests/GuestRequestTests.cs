using System.Net;
using System.Net.Http.Json;
using System.Text;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Tests;

public class GuestRequestTests
{
    [Fact]
    public void QuotesRequireCurrentPriceAvailabilityAndValidItems()
    {
        var menu = new Dictionary<int, MenuItem> { [1] = new() { Id = 1, Name = "Momo", Price = 100, IsAvailable = true } };
        var lines = new[] { new GuestQuoteLine(1, 2, 100, "mild") };
        var item = Assert.Single(GuestRequestRules.ValidateQuote(lines, menu));
        Assert.Equal("Momo", item.Name);
        Assert.Equal(100m, item.QuotedUnitPrice);
        Assert.Equal("mild", item.Notes);
        menu[1].Price = 120m;
        Assert.Throws<ConflictException>(() => GuestRequestRules.ValidateQuote(lines, menu));
        menu[1].Price = 100m;
        menu[1].IsAvailable = false;
        Assert.Throws<ConflictException>(() => GuestRequestRules.ValidateQuote(lines, menu));
        Assert.Throws<ConflictException>(() => GuestRequestRules.ValidateQuote(lines, new Dictionary<int, MenuItem>()));
        Assert.Throws<DomainException>(() => GuestRequestRules.ValidateQuote([new(1, 1, null, null)], menu));
        Assert.Throws<DomainException>(() => GuestRequestRules.ValidateQuote([new(1, 1, 100.001m, null)], menu));
        Assert.Throws<DomainException>(() => GuestRequestRules.ValidateQuote([new(1, 1, 100m, null), new(1, 1, 100m, null)], menu));
        Assert.Throws<DomainException>(() => GuestRequestRules.ValidateQuote([new(1, 1, 100m, new string('a', 301))], menu));
    }

    [Fact]
    public void DecisionsAreOneTimeAndRecordActor()
    {
        var request = new GuestRequest();
        var order = new Order();
        GuestRequestRules.Accept(request, order, "cashier");
        Assert.Equal("Accepted", request.Status);
        Assert.Same(order, request.Order);
        Assert.Equal("cashier", request.DecidedBy);
        Assert.NotNull(request.DecidedAt);
        Assert.Throws<ConflictException>(() => GuestRequestRules.Accept(request, new Order(), "cashier"));
        Assert.Throws<ConflictException>(() => GuestRequestRules.Reject(request, "reason", "cashier"));
        var rejected = new GuestRequest();
        Assert.Throws<DomainException>(() => GuestRequestRules.Reject(rejected, " ", "cashier"));
        Assert.Equal("Pending", rejected.Status);
        GuestRequestRules.Reject(rejected, " sold out ", "manager");
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("sold out", rejected.RejectionReason);
        Assert.Null(rejected.Order);
    }

    [Fact]
    public void StatusTokensAreRandomAndComparedWithStoredHashes()
    {
        var token = GuestAccessToken.Create();
        var other = GuestAccessToken.Create();
        Assert.NotEqual(token, other);
        Assert.True(GuestAccessToken.IsValidFormat(token));
        Assert.False(GuestAccessToken.IsValidFormat(new string('z', 64)));
        var hash = GuestAccessToken.Hash(token);
        Assert.Equal(32, hash.Length);
        Assert.True(GuestAccessToken.Matches(token, hash));
        Assert.False(GuestAccessToken.Matches(other, hash));
    }

    [Fact]
    public void RequestModelRequiresOneOrderPerRequestAndConcurrentDecisionProtection()
    {
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType(typeof(GuestRequest))!;
        Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.True(entity.GetIndexes().Single(x => x.Properties.SingleOrDefault()?.Name == "OrderId").IsUnique);
    }

    [Fact]
    public void AcceptanceTracksRequestAndNewOrderTogetherWithoutDatabaseAccess()
    {
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var request = new GuestRequest { Id = 12, AccessTokenHash = new byte[32] };
        db.Attach(request);
        var order = new Order { Items = [new() { MenuItemId = 2, Name = "Tea", Quantity = 1, UnitPrice = 20 }] };
        GuestRequestRules.Accept(request, order, "staff");
        db.Orders.Add(order);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, db.Entry(request).State);
        Assert.Equal(EntityState.Added, db.Entry(order).State);
        Assert.Equal(EntityState.Added, db.Entry(Assert.Single(order.Items)).State);
        Assert.NotNull(request.Order);
    }

    [Fact]
    public async Task GuestEndpointsRequireCsrfProtectStatusAndLimitSubmissions()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var body = new SubmitGuestRequest(null, [new(1, 1, 100m)]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/guest/requests", body)).StatusCode);
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        var created = await client.PostAsJsonAsync("/api/guest/requests", body);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var submission = (await created.Content.ReadFromJsonAsync<GuestSubmissionDto>())!;
        Assert.Equal("Pending", submission.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/guest/requests/{submission.Id}")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Guest-Request-Token", new string('B', 64));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/guest/requests/{submission.Id}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Guest-Request-Token");
        client.DefaultRequestHeaders.Add("X-Guest-Request-Token", submission.AccessToken);
        var statusResponse = await client.GetAsync($"/api/guest/requests/{submission.Id}");
        Assert.True(statusResponse.Headers.CacheControl!.NoStore);
        var status = await statusResponse.Content.ReadFromJsonAsync<GuestStatusDto>();
        Assert.Equal("Pending", status!.Status);
        Assert.Null(status.OrderId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/guest-requests")).StatusCode);
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/guest/requests", body)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/guest/requests", body)).StatusCode);
    }

    [Fact]
    public async Task GuestSubmissionRejectsBodiesLargerThanLimit()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        using var content = new StringContent("{\"padding\":\"" + new string('x', 65536) + "\"}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/guest/requests", content)).StatusCode);
    }

    [Fact]
    public async Task GuestSubmissionEnforcesLimitForUnknownLengthAndAllowsBoundary()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        const string prefix = "{\"padding\":\"";
        const string suffix = "\"}";
        var boundary = prefix + new string('x', 65536 - prefix.Length - suffix.Length) + suffix;
        using var allowed = new UnknownLengthContent(boundary);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/guest/requests", allowed)).StatusCode);
        using var oversized = new UnknownLengthContent(boundary + " ");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/guest/requests", oversized)).StatusCode);
    }

    [Fact]
    public async Task GuestDecisionsRequireCashierAndValidCsrf()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kitchen", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/guest-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/guest-requests/1/accept", null)).StatusCode);
    }

    [Fact]
    public async Task CashierAcceptResponseContainsLinkedDraftOrder()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/guest-requests/1/accept", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        var response = await client.PostAsync("/api/guest-requests/1/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<GuestAcceptanceDto>())!;
        Assert.Equal("Accepted", accepted.Request.Status);
        Assert.Equal("staff", accepted.Request.DecidedBy);
        Assert.Equal(accepted.Order.Id, accepted.Request.OrderId);
        Assert.Equal("Draft", accepted.Order.Status);
    }

    private record Csrf(string RequestToken);

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly string body;
        public UnknownLengthContent(string body)
        {
            this.body = body;
            Headers.ContentType = new("application/json");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    }
}

public sealed class FakeGuestService : IGuestRequestService
{
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public Task<GuestSubmissionDto> SubmitAsync(SubmitGuestRequest r, CancellationToken ct) => Task.FromResult(new GuestSubmissionDto(1, "Pending", Token));
    public Task<GuestStatusDto?> StatusAsync(int id, string? token, CancellationToken ct) =>
        Task.FromResult<GuestStatusDto?>(id == 1 && token == Token ? new(1, "Pending", null) : null);
    public Task<IReadOnlyList<GuestRequestDto>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<GuestRequestDto>>([]);
    public Task<GuestAcceptanceDto> AcceptAsync(int id, string actor, CancellationToken ct)
    {
        var request = new GuestRequestDto(id, null, "Accepted", DateTime.UtcNow, DateTime.UtcNow, actor, null, 2, []);
        var order = new OrderDto(2, null, "Draft", DateTime.UtcNow, [], 0, null, null, null, null, null);
        return Task.FromResult(new GuestAcceptanceDto(request, order));
    }
    public Task<GuestRequestDto> RejectAsync(int id, RejectGuestRequest r, string actor, CancellationToken ct) => throw new NotSupportedException();
}
