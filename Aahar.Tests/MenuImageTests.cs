using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace Aahar.Tests;

public sealed class MenuImageTests
{
    private static byte[] ImageBytes(SKEncodedImageFormat format, int width = 2, int height = 2)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return data.ToArray();
    }

    [Fact]
    public async Task StoreDecodesNormalizesAndAtomicallyReplaces()
    {
        var root = Path.Combine(Path.GetTempPath(), "aahar-images-" + Guid.NewGuid().ToString("N"));
        var store = new MenuImageStore(root, AppContext.BaseDirectory, AppContext.BaseDirectory);
        try
        {
            Assert.Null(await store.ReadAsync(1, default));
            store.Delete(1);
            await store.WriteAsync(1, "image/jpeg", new MemoryStream(ImageBytes(SKEncodedImageFormat.Jpeg)), default);
            var png = (await store.ReadAsync(1, default))!;
            using var codec = SKCodec.Create(SKData.CreateCopy(png));
            Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
            Assert.Throws<DomainException>(() => store.Delete(-1));
            await Assert.ThrowsAsync<DomainException>(() => store.WriteAsync(1, "image/jpeg", new MemoryStream(png), default));
            await Assert.ThrowsAsync<DomainException>(() => store.WriteAsync(1, "image/png", new MemoryStream([1, 2, 3]), default));
            await Assert.ThrowsAsync<DomainException>(() => store.WriteAsync(1, "image/png", new MemoryStream(png[..(png.Length / 2)]), default));
            await Assert.ThrowsAsync<DomainException>(() => store.WriteAsync(1, "image/png", new MemoryStream(new byte[MenuImageStore.MaxBytes + 1]), default));
            await Assert.ThrowsAsync<DomainException>(() => store.WriteAsync(1, "image/png",
                new MemoryStream(ImageBytes(SKEncodedImageFormat.Png, 2049, 1)), default));
            Assert.Equal(png, await store.ReadAsync(1, default));
            store.Delete(1);
            store.Delete(1);
            Assert.Null(await store.ReadAsync(1, default));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    [Fact]
    public void StorageMustBeAbsoluteAndOutsideApplication()
    {
        Assert.Throws<InvalidOperationException>(() => new MenuImageStore("images", AppContext.BaseDirectory, AppContext.BaseDirectory));
        Assert.Throws<InvalidOperationException>(() => new MenuImageStore(Path.Combine(AppContext.BaseDirectory, "images"),
            AppContext.BaseDirectory, AppContext.BaseDirectory));
        if (!OperatingSystem.IsWindows())
        {
            var link = Path.Combine(Path.GetTempPath(), "aahar-link-" + Guid.NewGuid().ToString("N"));
            Directory.CreateSymbolicLink(link, AppContext.BaseDirectory);
            try
            {
                Assert.Throws<InvalidOperationException>(() => new MenuImageStore(Path.Combine(link, "images"),
                    AppContext.BaseDirectory, AppContext.BaseDirectory));
            }
            finally { Directory.Delete(link); }
        }
    }

    [Fact]
    public async Task EndpointsEnforceRoleCsrfSizeAndGuestVisibility()
    {
        var root = Path.Combine(Path.GetTempPath(), "aahar-images-" + Guid.NewGuid().ToString("N"));
        var store = new MenuImageStore(root, AppContext.BaseDirectory, AppContext.BaseDirectory);
        try
        {
            await using var factory = new TestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMenuImageService>();
                services.AddSingleton<IMenuImageService>(new FakeImages(store));
            }));
            using var guest = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var staff = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var manager = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            await Login(staff, "staff");
            await Login(manager, "manager");
            var input = ImageBytes(SKEncodedImageFormat.Png);
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PutAsync("/api/menu/1/image", Content(input))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsync("/api/menu/1/image", Content(input))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync("/api/menu/1/image")).StatusCode);
            manager.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsync("/api/menu/1/image", Content(input))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await manager.DeleteAsync("/api/menu/1/image")).StatusCode);
            manager.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await manager.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await manager.PutAsync("/api/menu/1/image", Content(input, "text/plain"))).StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await manager.PutAsync("/api/menu/1/image", Content(new byte[MenuImageStore.MaxBytes + 1]))).StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await manager.PutAsync("/api/menu/1/image", new UnknownLengthContent(new byte[MenuImageStore.MaxBytes + 1]))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsync("/api/menu/1/image", Content([1, 2, 3]))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsync("/api/menu/99/image", Content(input))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync("/api/menu/1/image", Content(input))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsync("/api/menu/2/image", Content(input))).StatusCode);
            var response = await guest.GetAsync("/api/menu/1/image");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/menu/2/image")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/menu/2/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync("/api/menu/1/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync("/api/menu/1/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await manager.DeleteAsync("/api/menu/99/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/menu/1/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync("/api/menu/2/image")).StatusCode);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var path in Directory.GetFiles(root)) File.Delete(path);
                Directory.Delete(root);
            }
        }
    }

    private static ByteArrayContent Content(byte[] bytes, string type = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new(type);
        return content;
    }

    private static async Task Login(HttpClient client, string user)
    {
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user, "password"))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken);
    }

    private record Csrf(string RequestToken);

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] data;
        public UnknownLengthContent(byte[] data)
        {
            this.data = data;
            Headers.ContentType = new("image/png");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(data).AsTask();
    }

    private sealed class FakeImages(MenuImageStore store) : IMenuImageService
    {
        public async Task<byte[]?> GetAsync(int id, bool authenticated, CancellationToken ct) =>
            id == 1 || (id == 2 && authenticated) ? await store.ReadAsync(id, ct) : null;
        public async Task<bool> PutAsync(int id, string contentType, Stream data, CancellationToken ct)
        {
            if (id is not (1 or 2)) return false;
            await store.WriteAsync(id, contentType, data, ct);
            return true;
        }
        public Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            if (id is not (1 or 2)) return Task.FromResult(false);
            store.Delete(id);
            return Task.FromResult(true);
        }
    }
}
