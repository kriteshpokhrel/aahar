using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aahar.Tests;

public sealed class RestaurantProfileTests
{
    private sealed record Csrf(string RequestToken);
    [Fact]
    public async Task ProfilePersistsAndStaleUpdatesCannotOverwriteIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aahar-profile-{Guid.NewGuid():N}.json");
        using var store = new RestaurantProfileStore(path, AppContext.BaseDirectory, AppContext.BaseDirectory);
        try
        {
            Assert.Null((await store.ReadAsync(default)).Version);
            var first = await store.SaveAsync(new() { Name = " Test restaurant ", ReceiptFooter = "Thank you\nVisit again" }, default);
            var second = await store.SaveAsync(first with { Tagline = "Freshly prepared" }, default);
            await Assert.ThrowsAsync<ConflictException>(() => store.SaveAsync(first with { Name = "Stale edit" }, default));
            using var reopened = new RestaurantProfileStore(path, AppContext.BaseDirectory, AppContext.BaseDirectory);
            Assert.Equal(second, await reopened.ReadAsync(default));
            Assert.Equal("Test restaurant", second.Name);
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            await File.WriteAllTextAsync(path, "{");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(default));
            await File.WriteAllTextAsync(path, "{}");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(default));
            await File.WriteAllTextAsync(path, new string('x', 16 * 1024 + 1));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(default));
            if (!OperatingSystem.IsWindows())
            {
                File.Delete(path);
                File.CreateSymbolicLink(path, Path.Combine(AppContext.BaseDirectory, "profile.json"));
                await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(default));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ConcurrentEditsHaveOnlyOneWinner()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aahar-profile-{Guid.NewGuid():N}.json");
        using var store = new RestaurantProfileStore(path, AppContext.BaseDirectory, AppContext.BaseDirectory);
        try
        {
            var profile = await store.SaveAsync(new() { Name = "Test restaurant" }, default);
            async Task<bool> Save(string name)
            {
                try { await store.SaveAsync(profile with { Name = name }, default); return true; }
                catch (ConflictException) { return false; }
            }
            var results = await Task.WhenAll(Save("First edit"), Save("Second edit"));
            Assert.Single(results, x => x);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ProfileRejectsInvalidNamesContactsAndUnsafeStorage()
    {
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new()));
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new() { Name = new string('x', 101) }));
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new() { Name = "Test", Website = "javascript:alert(1)" }));
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new() { Name = "Test", Website = "https://user:password@example.invalid" }));
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new() { Name = "Test", Email = "not an email" }));
        Assert.Throws<DomainException>(() => RestaurantProfileRules.Normalize(new() { Name = "Test", ReceiptFooter = "hidden\0text" }));
        Assert.Throws<InvalidOperationException>(() => new RestaurantProfileStore("profile.json", AppContext.BaseDirectory, AppContext.BaseDirectory));
        Assert.Throws<InvalidOperationException>(() => new RestaurantProfileStore(Path.Combine(AppContext.BaseDirectory, "profile.json"),
            AppContext.BaseDirectory, AppContext.BaseDirectory));
    }

    [Fact]
    public async Task ProfileEndpointsArePublicForReadingButAdminAndCsrfProtectedForWriting()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aahar-profile-{Guid.NewGuid():N}.json");
        await using var factory = new TestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRestaurantProfileService>();
            services.AddSingleton<IRestaurantProfileService>(_ => new RestaurantProfileStore(path, AppContext.BaseDirectory, AppContext.BaseDirectory));
        }));
        try
        {
            using var guest = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            Assert.Null((await guest.GetFromJsonAsync<RestaurantProfile>("/api/restaurant-profile"))!.Version);
            var profile = new RestaurantProfile { Name = "Test restaurant" };
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PutAsJsonAsync("/api/restaurant-profile", profile)).StatusCode);
            foreach (var role in new[] { "staff", "manager", "kitchen", "admin" })
            {
                using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
                var token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(role, "password"))).StatusCode);
                client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
                if (role == "admin")
                    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/restaurant-profile", profile)).StatusCode);
                token = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
                var result = await client.PutAsJsonAsync("/api/restaurant-profile", profile);
                Assert.Equal(role == "admin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, result.StatusCode);
                if (role == "admin")
                {
                    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/restaurant-profile", profile)).StatusCode);
                    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/restaurant-profile", profile with { Name = "" })).StatusCode);
                    using var oversized = new StringContent(new string('x', 16 * 1024 + 1), System.Text.Encoding.UTF8, "application/json");
                    Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PutAsync("/api/restaurant-profile", oversized)).StatusCode);
                }
            }
            Assert.Equal("Test restaurant", (await guest.GetFromJsonAsync<RestaurantProfile>("/api/restaurant-profile"))!.Name);
        }
        finally { File.Delete(path); }
    }
}
