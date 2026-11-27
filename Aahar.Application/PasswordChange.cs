namespace Aahar.Application;

public sealed class ChangePasswordRequest
{
    public string? CurrentPassword { get; init; }
    public string? NewPassword { get; init; }
}

public interface IPasswordChangeService
{
    Task ChangeAsync(string username, ChangePasswordRequest request, CancellationToken ct);
}
