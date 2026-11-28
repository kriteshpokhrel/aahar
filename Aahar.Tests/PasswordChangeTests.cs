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
    [Theory]
    [InlineData("40001", HttpStatusCode.Conflict)]
    [InlineData("08006", HttpStatusCode.InternalServerError)]
    public async Task WrappedDatabaseErrorsReturnConflictOnlyForRecognizedSqlStates(string sqlState, HttpStatusCode expected)
    {
        var postgres = new Npgsql.PostgresException("Synthetic storage detail", "ERROR", "ERROR", sqlState);
        var service = new FakePasswordChangeService(new string('a', 12))
        {
            Failure = new InvalidOperationException("Transient failure",
                new DbUpdateException("Save failed", postgres))
        };
        await using var factory = new TestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordChangeService>();
            services.AddSingleton<IPasswordChangeService>(service);
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = new string('a', 12), NewPassword = new string('b', 12) });
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("Synthetic storage detail", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

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
        Assert.Single(UserQueries.UnchangedUser(users.AsQueryable(), 1, "old"));
        users[0].PasswordHash = "changed";
        Assert.Empty(UserQueries.UnchangedUser(users.AsQueryable(), 1, "old"));
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var sql = UserQueries.UnchangedUser(db.Users, 1, "old").ToQueryString();
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

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task ConditionalPasswordWriteCommitsOnlyWhenOriginalHashMatches(int updated)
    {
        var hasher = new PasswordHasher<User>();
        var user = new User { Id = 1, Username = "synthetic" };
        user.PasswordHash = hasher.HashPassword(user, new string('a', 12));
        var events = new List<string>();
        var repository = new PasswordRepository(user, events, updated);
        var service = new PasswordChangeService(repository, new PasswordUnitOfWork(events), hasher);
        var request = new ChangePasswordRequest { CurrentPassword = new string('a', 12), NewPassword = new string('b', 12) };

        if (updated == 1) await service.ChangeAsync(user.Username, request, default);
        else await Assert.ThrowsAsync<ConflictException>(() => service.ChangeAsync(user.Username, request, default));

        Assert.Equal(updated == 1 ? new[] { "begin", "update", "commit", "dispose" } : new[] { "begin", "update", "dispose" }, events);
        Assert.Equal(user.PasswordHash, repository.ExpectedHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, repository.ReplacementHash!, request.NewPassword));
    }

    [Fact]
    public async Task InvalidCurrentPasswordDoesNotStartTransactionOrWrite()
    {
        var user = new User { Id = 1, Username = "synthetic" };
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, new string('a', 12));
        var events = new List<string>();
        var repository = new PasswordRepository(user, events, 1);
        var service = new PasswordChangeService(repository, new PasswordUnitOfWork(events), hasher);

        await Assert.ThrowsAsync<DomainException>(() => service.ChangeAsync(user.Username,
            new() { CurrentPassword = "wrong", NewPassword = new string('b', 12) }, default));
        Assert.Empty(events);
        Assert.Null(repository.ReplacementHash);
    }

    private sealed class PasswordRepository(User user, List<string> events, int updated) : IUserRepository
    {
        public string? ExpectedHash { get; private set; }
        public string? ReplacementHash { get; private set; }
        public Task<User?> FindForPasswordChangeAsync(string username, CancellationToken ct) => Task.FromResult<User?>(user);
        public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) => throw new NotSupportedException();
        public void Add(User value) => throw new NotSupportedException();
        public Task<int> ChangePasswordIfUnchangedAsync(int id, string oldHash, string newHash, CancellationToken ct)
        {
            Assert.Equal(user.Id, id);
            ExpectedHash = oldHash;
            ReplacementHash = newHash;
            events.Add("update");
            return Task.FromResult(updated);
        }
    }

    private sealed class PasswordUnitOfWork(List<string> events) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ITransaction> BeginSerializableAsync(CancellationToken ct)
        {
            events.Add("begin");
            return Task.FromResult<ITransaction>(new PasswordTransaction(events));
        }
        private sealed class PasswordTransaction(List<string> events) : ITransaction
        {
            public Task CommitAsync(CancellationToken ct) { events.Add("commit"); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { events.Add("dispose"); return ValueTask.CompletedTask; }
        }
    }

    private sealed class FakePasswordChangeService : IPasswordChangeService
    {
        public readonly User User = new() { Id = 1, Username = "staff" };
        public readonly PasswordHasher<User> Hasher = new();
        public string? Actor { get; private set; }
        public bool Conflict { get; init; }
        public Exception? Failure { get; init; }
        public FakePasswordChangeService(string password) => User.PasswordHash = Hasher.HashPassword(User, password);
        public Task ChangeAsync(string username, ChangePasswordRequest request, CancellationToken ct)
        {
            Actor = username;
            if (Failure is not null) throw Failure;
            if (Conflict) throw new ConflictException("Password changed concurrently; sign in again and retry.");
            User.PasswordHash = PasswordChange.CreateHash(User, request, Hasher);
            return Task.CompletedTask;
        }
    }
}
