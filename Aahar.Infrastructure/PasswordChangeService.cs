using Aahar.Application;
using Aahar.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public static class PasswordChange
{
    public static string CreateHash(User? user, ChangePasswordRequest request, IPasswordHasher<User> hasher)
    {
        if (request.NewPassword is not { Length: >= 12 and <= 256 })
            throw new DomainException("New password must be between 12 and 256 characters.");
        if (request.CurrentPassword is not { Length: > 0 and <= 256 } || user is null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new DomainException("Current password is incorrect.");
        return hasher.HashPassword(user, request.NewPassword);
    }

    public static IQueryable<User> UnchangedUser(IQueryable<User> users, int id, string hash) =>
        users.Where(x => x.Id == id && x.PasswordHash == hash);
}

public sealed class PasswordChangeService(RestaurantDbContext db, IPasswordHasher<User> hasher) : IPasswordChangeService
{
    public async Task ChangeAsync(string username, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username, ct);
        var newHash = PasswordChange.CreateHash(user, request, hasher);
        var updated = await PasswordChange.UnchangedUser(db.Users, user!.Id, user.PasswordHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, newHash), ct);
        if (updated != 1) throw new ConflictException("Password changed concurrently; sign in again and retry.");
    }
}
