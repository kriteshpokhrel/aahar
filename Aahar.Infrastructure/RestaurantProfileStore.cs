using System.Text.Json;
using Aahar.Application;
using Aahar.Domain;

namespace Aahar.Infrastructure;

public sealed class RestaurantProfileStore : IRestaurantProfileService, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;

    public RestaurantProfileStore(string filePath, string contentRoot, string executableRoot)
    {
        if (!Path.IsPathFullyQualified(filePath) || !filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restaurant profile must use an absolute JSON file path.");
        var directory = LocalStoragePath.ResolveDirectory(Path.GetDirectoryName(filePath)!);
        path = Path.Combine(directory, Path.GetFileName(filePath));
        if (LocalStoragePath.IsInside(path, LocalStoragePath.ResolveDirectory(contentRoot)) ||
            LocalStoragePath.IsInside(path, LocalStoragePath.ResolveDirectory(executableRoot)))
            throw new InvalidOperationException("Restaurant profile must be outside application directories.");
    }

    public async Task<RestaurantProfile> ReadAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await ReadFileAsync(ct); }
        finally { gate.Release(); }
    }

    private async Task<RestaurantProfile> ReadFileAsync(CancellationToken ct)
    {
        try
        {
            if (new FileInfo(path).LinkTarget is not null)
                throw new InvalidDataException("Restaurant profile must not be a symbolic link.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 16 * 1024) throw new InvalidDataException("Stored restaurant profile exceeds the size limit.");
            var profile = await JsonSerializer.DeserializeAsync<RestaurantProfile>(stream, Json, ct);
            if (profile?.Version is null || profile.Version == Guid.Empty)
                throw new InvalidDataException("Stored restaurant profile is incomplete. Restore a valid profile file.");
            try { return RestaurantProfileRules.Normalize(profile); }
            catch (DomainException) { throw new InvalidDataException("Stored restaurant profile is invalid. Restore a valid profile file."); }
        }
        catch (FileNotFoundException) { return new RestaurantProfile(); }
        catch (DirectoryNotFoundException) { return new RestaurantProfile(); }
        catch (JsonException) { throw new InvalidDataException("Stored restaurant profile is malformed. Restore a valid profile file."); }
    }

    public async Task<RestaurantProfile> SaveAsync(RestaurantProfile profile, CancellationToken ct)
    {
        var normalized = RestaurantProfileRules.Normalize(profile);
        await gate.WaitAsync(ct);
        try
        {
            var current = await ReadFileAsync(ct);
            if (normalized.Version != current.Version)
                throw new ConflictException("Restaurant settings changed. Reload before saving again.");
            var saved = normalized with { Version = Guid.NewGuid() };
            var directory = Path.GetDirectoryName(path)!;
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
            else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var temporary = Path.Combine(directory, $".restaurant-{Guid.NewGuid():N}.tmp");
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using (var stream = new FileStream(temporary, options))
                {
                    await JsonSerializer.SerializeAsync(stream, saved, Json, ct);
                    await stream.FlushAsync(ct);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally { File.Delete(temporary); }
            return saved;
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}
