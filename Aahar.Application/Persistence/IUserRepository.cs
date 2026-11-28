using Aahar.Domain;

namespace Aahar.Application;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<User?> FindForPasswordChangeAsync(string username, CancellationToken ct);
    void Add(User user);
    Task<int> ChangePasswordIfUnchangedAsync(int id, string oldHash, string newHash, CancellationToken ct);
}
