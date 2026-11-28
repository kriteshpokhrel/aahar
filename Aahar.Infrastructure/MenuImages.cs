using Aahar.Application;
using Aahar.Domain;
using SkiaSharp;

namespace Aahar.Infrastructure;

public sealed class MenuImageStore
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private readonly string root;

    public MenuImageStore(string path, string contentRoot, string executableRoot)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Menu image directory must be an absolute path.");
        root = LocalStoragePath.ResolveDirectory(path);
        if (LocalStoragePath.IsInside(root, LocalStoragePath.ResolveDirectory(contentRoot)) || LocalStoragePath.IsInside(root, LocalStoragePath.ResolveDirectory(executableRoot)))
            throw new InvalidOperationException("Menu image directory must be outside application content and executable directories.");
    }

    private string ImagePath(int id)
    {
        if (id <= 0) throw new DomainException("Invalid menu item.");
        return Path.Combine(root, $"menu-{id}.png");
    }

    public async Task<byte[]?> ReadAsync(int id, CancellationToken ct)
    {
        try { return await File.ReadAllBytesAsync(ImagePath(id), ct); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task WriteAsync(int id, string contentType, Stream data, CancellationToken ct)
    {
        var destination = ImagePath(id);
        using var source = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await data.ReadAsync(buffer, ct)) != 0)
        {
            if (source.Length + read > MaxBytes) throw new DomainException("Image exceeds the size limit.");
            await source.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        var bytes = source.ToArray();
        using var encoded = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(encoded);
        if (codec is null) throw new DomainException("Invalid image data.");
        if ((contentType == "image/png" && codec.EncodedFormat != SKEncodedImageFormat.Png) ||
            (contentType == "image/jpeg" && codec.EncodedFormat != SKEncodedImageFormat.Jpeg) ||
            contentType is not ("image/png" or "image/jpeg"))
            throw new DomainException("Image content must match its PNG or JPEG content type.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 2048 || info.Height > 2048 ||
            (long)info.Width * info.Height > 4_000_000)
            throw new DomainException("Image dimensions exceed the limit.");
        using var bitmap = new SKBitmap(info.Width, info.Height);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new DomainException("Invalid image data.");
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode menu image.");
        Directory.CreateDirectory(root);
        var temp = Path.Combine(root, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, png.ToArray(), ct);
            File.Move(temp, destination, overwrite: true);
        }
        finally { File.Delete(temp); }
    }

    public void Delete(int id)
    {
        try { File.Delete(ImagePath(id)); }
        catch (DirectoryNotFoundException) { }
    }
}

public sealed class MenuImageService(IMenuRepository repository, MenuImageStore store) : IMenuImageService
{
    public async Task<byte[]?> GetAsync(int id, bool authenticated, CancellationToken ct)
    {
        if (id <= 0 || !await repository.ExistsAsync(id, !authenticated, ct))
            return null;
        return await store.ReadAsync(id, ct);
    }

    public async Task<bool> PutAsync(int id, string contentType, Stream data, CancellationToken ct)
    {
        if (id <= 0 || !await repository.ExistsAsync(id, false, ct)) return false;
        await store.WriteAsync(id, contentType, data, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        if (id <= 0 || !await repository.ExistsAsync(id, false, ct)) return false;
        store.Delete(id);
        return true;
    }
}
