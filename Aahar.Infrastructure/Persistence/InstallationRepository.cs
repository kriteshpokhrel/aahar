using Aahar.Application;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class InstallationRepository(RestaurantDbContext db) : IInstallationRepository
{
    public async Task LockBootstrapAsync(CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094799698)", ct);
    public async Task LockDemoSeedAsync(CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094799699)", ct);
    public Task<bool> UsersExistAsync(CancellationToken ct) => db.Users.AnyAsync(ct);
    public async Task<bool> RestaurantDataExistsAsync(CancellationToken ct) =>
        await db.MenuItems.AnyAsync(ct) || await db.Tables.AnyAsync(ct) ||
        await db.Ingredients.AnyAsync(ct) || await db.Orders.AnyAsync(ct) ||
        await db.GuestRequests.AnyAsync(ct) || await db.Suppliers.AnyAsync(ct);
}
