using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aahar.Tests;

public sealed class PasswordChangeTests
{
    [Fact]
    public void VerifiesCurrentPasswordAndHashesPolicyCompliantNewPassword()
    {
        var hasher = new PasswordHasher<User>();
        var oldPassword = new string('a', 12);
        var newPassword = new string('b', 12);
        var user = new User { Id = 1, Username = "staff" };
        user.PasswordHash = hasher.HashPassword(user, oldPassword);
        var hash = user.PasswordHash;
        Assert.Throws<DomainException>(() => PasswordChange.CreateHash(user, new() { CurrentPassword = "wrong", NewPassword = newPassword }, hasher));
        Assert.Throws<DomainException>(() => PasswordChange.CreateHash(user, new() { CurrentPassword = oldPassword, NewPassword = new string('c', 11) }, hasher));
        Assert.Throws<DomainException>(() => PasswordChange.CreateHash(user, new() { CurrentPassword = oldPassword, NewPassword = new string('c', 257) }, hasher));
        Assert.Throws<DomainException>(() => PasswordChange.CreateHash(user, new() { CurrentPassword = null, NewPassword = newPassword }, hasher));
        Assert.Throws<DomainException>(() => PasswordChange.CreateHash(null, new() { CurrentPassword = oldPassword, NewPassword = newPassword }, hasher));
        Assert.Equal(hash, user.PasswordHash);
        var replacement = PasswordChange.CreateHash(user, new() { CurrentPassword = oldPassword, NewPassword = newPassword }, hasher);
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, replacement, oldPassword));
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, replacement, newPassword));
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user,
            PasswordChange.CreateHash(user, new() { CurrentPassword = oldPassword, NewPassword = new string('c', 256) }, hasher), new string('c', 256)));
    }

    [Fact]
    public void ConcurrentChangeFilterMatchesOnlyOriginalUserAndHash()
    {
        var users = new[] { new User { Id = 1, PasswordHash = "old" }, new User { Id = 2, PasswordHash = "old" } };
        Assert.Single(PasswordChange.UnchangedUser(users.AsQueryable(), 1, "old"));
        users[0].PasswordHash = "changed";
        Assert.Empty(PasswordChange.UnchangedUser(users.AsQueryable(), 1, "old"));
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var sql = PasswordChange.UnchangedUser(db.Users, 1, "old").ToQueryString();
        Assert.Contains("\"Id\"", sql);
        Assert.Contains("\"PasswordHash\"", sql);
    }

    [Fact]
    public async Task ChangeRequiresAuthenticationAndCsrfAndSignsOutOnlyOnSuccess()
    {
        var password = new string('a', 12);
        var replacement = new string('b', 12);
        var service = new FakePasswordChangeService(password);
        await using var factory = new TestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordChangeService>();
            services.AddSingleton<IPasswordChangeService>(service);
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var request = new ChangePasswordRequest { CurrentPassword = password, NewPassword = replacement };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/change-password", request)).StatusCode);
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/change-password", request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        var wrong = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = replacement });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.DoesNotContain(replacement, await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest { CurrentPassword = password, NewPassword = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal("staff", service.Actor);
        Assert.Equal(PasswordVerificationResult.Failed, service.Hasher.VerifyHashedPassword(service.User, service.User.PasswordHash, password));
        Assert.NotEqual(PasswordVerificationResult.Failed, service.Hasher.VerifyHashedPassword(service.User, service.User.PasswordHash, replacement));
    }

    [Fact]
    public async Task ConflictsLeaveSessionSignedInAndAttemptsAreThrottled()
    {
        var service = new FakePasswordChangeService(new string('a', 12)) { Conflict = true };
        await using var factory = new TestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordChangeService>();
            services.AddSingleton<IPasswordChangeService>(service);
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("kitchen", "password"))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        var request = new ChangePasswordRequest { CurrentPassword = new string('a', 12), NewPassword = new string('b', 12) };
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/change-password", request)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/change-password", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private record Csrf(string RequestToken);

    private sealed class FakePasswordChangeService : IPasswordChangeService
    {
        public readonly User User = new() { Id = 1, Username = "staff" };
        public readonly PasswordHasher<User> Hasher = new();
        public string? Actor { get; private set; }
        public bool Conflict { get; init; }
        public FakePasswordChangeService(string password) => User.PasswordHash = Hasher.HashPassword(User, password);
        public Task ChangeAsync(string username, ChangePasswordRequest request, CancellationToken ct)
        {
            Actor = username;
            if (Conflict) throw new ConflictException("Password changed concurrently; sign in again and retry.");
            User.PasswordHash = PasswordChange.CreateHash(User, request, Hasher);
            return Task.CompletedTask;
        }
    }
}
