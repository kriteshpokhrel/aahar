namespace Aahar.Application;

public interface IInstallationRepository
{
    Task LockBootstrapAsync(CancellationToken ct);
    Task LockDemoSeedAsync(CancellationToken ct);
    Task<bool> UsersExistAsync(CancellationToken ct);
    Task<bool> RestaurantDataExistsAsync(CancellationToken ct);
}
