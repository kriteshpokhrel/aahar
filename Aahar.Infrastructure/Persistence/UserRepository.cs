using Aahar.Application;
using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public static class UserQueries
{
    public static IQueryable<User> UnchangedUser(IQueryable<User> users, int id, string hash) =>
        users.Where(x => x.Id == id && x.PasswordHash == hash);
}

public sealed class UserRepository(RestaurantDbContext db) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(x => x.Username == username, ct);
    public Task<User?> FindForPasswordChangeAsync(string username, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username, ct);
    public void Add(User user) => db.Users.Add(user);
    public Task<int> ChangePasswordIfUnchangedAsync(int id, string oldHash, string newHash, CancellationToken ct) =>
        UserQueries.UnchangedUser(db.Users, id, oldHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, newHash), ct);
}
