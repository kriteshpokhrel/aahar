using Aahar.Application;
using Aahar.Domain;
using Microsoft.AspNetCore.Identity;

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

}

public sealed class PasswordChangeService(IUserRepository repository, IUnitOfWork unitOfWork, IPasswordHasher<User> hasher) : IPasswordChangeService
{
    public async Task ChangeAsync(string username, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await repository.FindForPasswordChangeAsync(username, ct);
        var newHash = PasswordChange.CreateHash(user, request, hasher);
        await using var tx = await unitOfWork.BeginSerializableAsync(ct);
        var updated = await repository.ChangePasswordIfUnchangedAsync(user!.Id, user.PasswordHash, newHash, ct);
        if (updated != 1) throw new ConflictException("Password changed concurrently; sign in again and retry.");
        await tx.CommitAsync(ct);
    }
}
