using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SkiaSharp;

namespace Aahar.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AAHAR_TEST_CONNECTION")))
            Skip = "Set AAHAR_TEST_CONNECTION to the explicitly provisioned local aahar_test database.";
    }
}

[Collection("PostgreSQL")]
public class PostgresWorkflowTests
{
    [PostgresFact]
    public async Task OrderAndGuestWorkflowsPersistAndEnforceAuthorization()
    {
        var connection = Environment.GetEnvironmentVariable("AAHAR_TEST_CONNECTION")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("aahar_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1", "::1" });

        var suffix = Guid.NewGuid().ToString("N");
        var username = "test-" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql(connection).Options;
        await using (var db = new RestaurantDbContext(options))
        {
            var user = new User { Username = username, Role = "Admin" };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using var imageDirectory = new TestImageDirectory();
        await using var factory = new RealDatabaseFactory(connection, imageDirectory.Path);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/orders")).StatusCode);
        await RefreshCsrf(client);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password))).EnsureSuccessStatusCode();
        await RefreshCsrf(client);

        var menu = await Read<MenuDto>(await client.PostAsJsonAsync("/api/menu",
            new MenuRequest("Item " + suffix, null, "Test", 125m, true)));
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var imageContent = new ByteArrayContent(encoded.ToArray());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsync($"/api/menu/{menu.Id}/image", imageContent)).StatusCode);
        Assert.Equal("image/png", (await client.GetAsync($"/api/menu/{menu.Id}/image")).Content.Headers.ContentType!.MediaType);
        var ingredient = await Read<IngredientDto>(await client.PostAsJsonAsync("/api/ingredients",
            new IngredientRequest("Ingredient " + suffix, "kg", 5m)));
        (await client.PutAsJsonAsync($"/api/menu/{menu.Id}/recipe",
            new RecipeRequest([new RecipeLine(ingredient.Id, 1m)]))).EnsureSuccessStatusCode();

        var order = await Read<OrderDto>(await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(null, [new OrderLineRequest(menu.Id, 2, "mild")])));
        var sent = await Read<OrderDto>(await client.PostAsync($"/api/orders/{order.Id}/send", null));
        Assert.Equal("Sent", sent.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/orders/{order.Id}/send", null)).StatusCode);
        var ingredients = await client.GetFromJsonAsync<List<IngredientDto>>("/api/ingredients");
        Assert.Equal(3m, ingredients!.Single(x => x.Id == ingredient.Id).Stock);
        var tickets = await client.GetFromJsonAsync<List<KitchenTicketDto>>("/api/kitchen/tickets");
        var ticket = tickets!.Single(x => x.OrderId == order.Id);
        Assert.Equal("mild", Assert.Single(ticket.Items).Notes);
        (await client.PostAsync($"/api/kitchen/tickets/{ticket.Id}/prepare", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/kitchen/tickets/{ticket.Id}/ready", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/orders/{order.Id}/complete", null)).EnsureSuccessStatusCode();
        var paid = await Read<OrderDto>(await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new PaymentRequest("Cash")));
        Assert.Equal("Paid", paid.Status);
        Assert.Equal(250m, paid.Payment!.Amount);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new PaymentRequest("Cash"))).StatusCode);
        var localPayment = TimeZoneInfo.ConvertTimeFromUtc(paid.Payment.PaidAt, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu"));
        var date = DateOnly.FromDateTime(localPayment).ToString("yyyy-MM-dd");
        var report = await client.GetFromJsonAsync<BestSellingReport>(
            $"/api/reports/best-selling?fromDate={date}&toDate={date}&limit=100");
        Assert.NotEmpty(report!.Items);
        Assert.All(report.Items, item => Assert.True(item.Quantity > 0 && item.Revenue >= 0));
        var sales = await client.GetFromJsonAsync<PaidSalesReport>($"/api/reports/sales?fromDate={date}&toDate={date}");
        Assert.True(sales!.OrdersPaid >= 1 && sales.Total >= 250m);

        var first = await Read<OrderDto>(await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(null, [new OrderLineRequest(menu.Id, 2)])));
        var second = await Read<OrderDto>(await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(null, [new OrderLineRequest(menu.Id, 2)])));
        var sends = await Task.WhenAll(
            client.PostAsync($"/api/orders/{first.Id}/send", null),
            client.PostAsync($"/api/orders/{second.Id}/send", null));
        Assert.Single(sends, response => response.IsSuccessStatusCode);
        Assert.Single(sends, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        ingredients = await client.GetFromJsonAsync<List<IngredientDto>>("/api/ingredients");
        Assert.Equal(1m, ingredients!.Single(x => x.Id == ingredient.Id).Stock);

        var table = await Read<TableDto>(await client.PostAsJsonAsync("/api/tables",
            new TableRequest(RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue), 4)));
        var competingOrders = await Task.WhenAll(
            client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(table.Id, [new OrderLineRequest(menu.Id, 1)])),
            client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(table.Id, [new OrderLineRequest(menu.Id, 1)])));
        Assert.Single(competingOrders, response => response.IsSuccessStatusCode);
        Assert.Single(competingOrders, response => response.StatusCode == HttpStatusCode.Conflict);

        var kitchenPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        (await client.PostAsJsonAsync("/api/users", new CreateUserRequest("kitchen-" + suffix, kitchenPassword, "Kitchen")))
            .EnsureSuccessStatusCode();
        using var kitchen = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await RefreshCsrf(kitchen);
        (await kitchen.PostAsJsonAsync("/api/auth/login", new LoginRequest("kitchen-" + suffix, kitchenPassword)))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await kitchen.GetAsync("/api/ingredients")).StatusCode);

        using var guest = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await RefreshCsrf(guest);
        var submission = await Read<GuestSubmissionDto>(await guest.PostAsJsonAsync("/api/guest/requests",
            new SubmitGuestRequest(null, [new GuestRequestLine(menu.Id, 1, 125m)])));
        guest.DefaultRequestHeaders.Add("X-Guest-Request-Token", submission.AccessToken);
        var before = await guest.GetFromJsonAsync<GuestStatusDto>($"/api/guest/requests/{submission.Id}");
        Assert.Equal("Pending", before!.Status);
        var accepted = await Read<GuestAcceptanceDto>(await client.PostAsync($"/api/guest-requests/{submission.Id}/accept", null));
        Assert.Equal("Draft", accepted.Order.Status);
        var after = await guest.GetFromJsonAsync<GuestStatusDto>($"/api/guest/requests/{submission.Id}");
        Assert.Equal("Accepted", after!.Status);
        Assert.Equal(accepted.Order.Id, after.OrderId);

        var newPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = password, NewPassword = newPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, newPassword))).StatusCode);

        await using var verify = new RestaurantDbContext(options);
        var existingMenuCount = await verify.MenuItems.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoDataSeeder.SeedAsync(verify));
        Assert.Equal(existingMenuCount, await verify.MenuItems.CountAsync());
    }

    private static async Task RefreshCsrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var token = await client.GetFromJsonAsync<Csrf>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record Csrf(string RequestToken);
}

internal sealed class RealDatabaseFactory(string connection, string imageDirectory) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Aahar"] = connection,
                ["MenuImages:Directory"] = imageDirectory
            }));
        builder.ConfigureServices(services => services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, LoopbackFilter>());
    }
}

internal sealed class TestImageDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aahar-test-images-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
