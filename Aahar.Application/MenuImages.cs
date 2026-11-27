namespace Aahar.Application;

public interface IMenuImageService
{
    Task<byte[]?> GetAsync(int id, bool authenticated, CancellationToken ct);
    Task<bool> PutAsync(int id, string contentType, Stream data, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
