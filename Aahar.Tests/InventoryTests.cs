using System.Net;
using System.Net.Http.Json;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Tests;

public class InventoryTests
{
    [Fact]
    public void SupplierAndThresholdChangesCaptureReasonAndActor()
    {
        var supplier = new Supplier();
        Assert.Throws<DomainException>(() => InventoryRules.ChangeSupplier(supplier, "Vendor", null, " ", "admin"));
        Assert.Equal("", supplier.Name);
        var change = InventoryRules.ChangeSupplier(supplier, " Vendor ", "  123 ", " initial ", "admin");
        Assert.Equal("Vendor", supplier.Name);
        Assert.Equal("123", change.Contact);
        Assert.Equal("admin", change.Actor);
        Assert.Equal("initial", change.Reason);
        var ingredient = new Ingredient { Stock = 2m };
        var initial = InventoryRules.ChangeThreshold(ingredient, 2m, "initial", "admin", initial: true);
        Assert.Null(initial.PreviousThreshold);
        Assert.True(ingredient.IsLowStock);
        var update = InventoryRules.ChangeThreshold(ingredient, 1m, "adjusted", "manager");
        Assert.Equal(2m, update.PreviousThreshold);
        Assert.Equal("manager", update.Actor);
        Assert.False(ingredient.IsLowStock);
        Assert.Throws<DomainException>(() => InventoryRules.ChangeThreshold(ingredient, 1m, "same", "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.ChangeThreshold(ingredient, 0.0001m, "invalid", "manager"));
    }

    [Fact]
    public void WasteRequiresCorrectUnitReasonAndAvailableStock()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Unit = "kg", Stock = 5m };
        Assert.Throws<DomainException>(() => InventoryRules.Waste(rice, 1, "g", "expired", "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.Waste(rice, 6, "kg", "expired", "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.Waste(rice, 1, "kg", " ", "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.Waste(rice, 0.0001m, "kg", "expired", "manager"));
        Assert.Equal(5m, rice.Stock);
        var movement = InventoryRules.Waste(rice, 2.5m, "kg", " expired ", "manager");
        Assert.Equal(2.5m, rice.Stock);
        Assert.Equal(-2.5m, movement.QuantityChange);
        Assert.Equal(2.5m, movement.BalanceAfter);
        Assert.Equal("Waste", movement.Kind);
        Assert.Equal("expired", movement.Reason);
        Assert.Equal("manager", movement.Actor);
    }

    [Fact]
    public void PurchaseValidatesAllLinesBeforeChangingAnyStockAndSnapshotsCosts()
    {
        var supplier = new Supplier { Id = 4, Name = "Vendor" };
        var rice = new Ingredient { Id = 1, Name = "Rice", Unit = "kg", Stock = 5m };
        var salt = new Ingredient { Id = 2, Name = "Salt", Unit = "kg", Stock = 1m };
        var ingredients = new Dictionary<int, Ingredient> { [1] = rice, [2] = salt };
        Assert.Throws<DomainException>(() => InventoryRules.Receive(supplier, "INV-1", "delivery",
            [new(1, 1m, "kg", 10m), new(2, 1m, "g", 5m)], ingredients, "manager"));
        Assert.Equal(5m, rice.Stock);
        Assert.Equal(1m, salt.Stock);
        Assert.Throws<DomainException>(() => InventoryRules.Receive(supplier, "INV-1", "delivery",
            [new(1, 1m, "kg", 10m), new(1, 1m, "kg", 10m)], ingredients, "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.Receive(supplier, "INV-1", "delivery",
            [new(1, 1m, "kg", null)], ingredients, "manager"));
        Assert.Throws<DomainException>(() => InventoryRules.Receive(supplier, "INV-1", "delivery",
            [new(1, 1m, "kg", 1.001m)], ingredients, "manager"));
        var (receipt, movements) = InventoryRules.Receive(supplier, " INV-1 ", " delivery ",
            [new(1, 0.001m, "kg", 5m), new(2, 1.5m, "kg", 3m)], ingredients, "manager");
        Assert.Equal("INV-1", receipt.Reference);
        Assert.Equal(4.51m, receipt.Total);
        Assert.Equal(0.01m, receipt.Lines[0].LineTotal);
        Assert.Equal("Rice", receipt.Lines[0].IngredientName);
        Assert.Equal("kg", receipt.Lines[0].Unit);
        Assert.Equal(5.001m, rice.Stock);
        Assert.Equal(2.5m, salt.Stock);
        Assert.Equal(2, movements.Count);
        Assert.Same(receipt.Lines[0], movements[0].PurchaseReceiptLine);
        Assert.Equal("Purchase", movements[0].Kind);
        Assert.Equal(0.001m, movements[0].QuantityChange);
        Assert.Equal("delivery", movements[0].Reason);
        supplier.Name = "Renamed";
        rice.Name = "Renamed";
        Assert.Equal("Vendor", receipt.SupplierName);
        Assert.Equal("Rice", receipt.Lines[0].IngredientName);
    }

    [Fact]
    public void PurchaseRejectsOverflowWithoutChangingStock()
    {
        var rice = new Ingredient { Id = 1, Name = "Rice", Unit = "kg", Stock = InventoryRules.MaxQuantity };
        var ingredients = new Dictionary<int, Ingredient> { [1] = rice };
        Assert.Throws<DomainException>(() => InventoryRules.Receive(new Supplier(), "INV", "delivery",
            [new(1, 1m, "kg", 1m)], ingredients, "manager"));
        Assert.Equal(InventoryRules.MaxQuantity, rice.Stock);
        rice.Stock = 0;
        Assert.Throws<DomainException>(() => InventoryRules.Receive(new Supplier(), "INV", "delivery",
            [new(1, 1000m, "kg", InventoryRules.MaxMoney)], ingredients, "manager"));
        Assert.Equal(0m, rice.Stock);
    }

    [Fact]
    public void InventoryGraphAndConstraintsTrackWithoutDatabaseAccess()
    {
        using var db = new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=unused;Password=unused").Options);
        var supplier = new Supplier { Id = 1, Name = "Vendor" };
        var ingredient = new Ingredient { Id = 1, Name = "Rice", Unit = "kg", Stock = 2 };
        db.Attach(supplier);
        db.Attach(ingredient);
        var (receipt, movements) = InventoryRules.Receive(supplier, "ref", "delivery",
            [new(1, 1, "kg", 10)], new Dictionary<int, Ingredient> { [1] = ingredient }, "admin");
        db.PurchaseReceipts.Add(receipt);
        db.StockMovements.AddRange(movements);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, db.Entry(ingredient).State);
        Assert.Equal(EntityState.Added, db.Entry(receipt).State);
        Assert.Equal(EntityState.Added, db.Entry(Assert.Single(receipt.Lines)).State);
        Assert.Equal(EntityState.Added, db.Entry(Assert.Single(movements)).State);
        var receiptType = db.Model.FindEntityType(typeof(PurchaseReceipt))!;
        Assert.True(receiptType.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["SupplierId", "Reference"])).IsUnique);
        Assert.True(db.Model.FindEntityType(typeof(Supplier))!.FindProperty("Revision")!.IsConcurrencyToken);
        var movementType = db.Model.FindEntityType(typeof(StockMovement))!;
        Assert.True(movementType.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["PurchaseReceiptLineId"])).IsUnique);
        Assert.Equal(new StockMovementDto(0, 1, null, "Purchase", 1, 3, "admin", movements[0].OccurredAt, "delivery", null),
            movements[0].Adapt<StockMovementDto>());
    }

    [Fact]
    public async Task InventoryEndpointsRequireManagerAndCsrf()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/suppliers")).StatusCode);
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("staff", "password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/suppliers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ingredients/1/waste", new WasteRequest(1, "kg", "expired"))).StatusCode);
    }

    [Fact]
    public async Task ManagerReceivesPurchaseWithActorAndSnapshot()
    {
        await using var factory = new TestFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("manager", "password"))).StatusCode);
        var body = new PurchaseReceiptRequest(1, "INV-1", "delivery", [new(1, 2, "kg", 3)]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/purchase-receipts", body)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        csrf = (await client.GetFromJsonAsync<Csrf>("/api/auth/csrf"))!.RequestToken;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        var response = await client.PostAsJsonAsync("/api/purchase-receipts", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<PurchaseReceiptDto>())!;
        Assert.Equal("manager", receipt.Actor);
        Assert.Equal(6m, receipt.Total);
        Assert.Equal(3m, Assert.Single(receipt.Lines).UnitCost);
    }
    private record Csrf(string RequestToken);
}

public sealed class FakeInventoryService : IInventoryService
{
    public Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierDto>>([]);
    public Task<SupplierDto> SaveSupplierAsync(int? id, SupplierRequest request, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<SupplierChangeDto>> SupplierHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<IngredientDto>> LowStockAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> ChangeThresholdAsync(int id, ThresholdRequest request, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ThresholdChangeDto>> ThresholdHistoryAsync(int id, long? beforeId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<IngredientDto> WasteAsync(int id, WasteRequest request, string actor, CancellationToken ct) => throw new NotSupportedException();
    public Task<PurchaseReceiptDto> ReceiveAsync(PurchaseReceiptRequest r, string actor, CancellationToken ct) =>
        Task.FromResult(new PurchaseReceiptDto(1, r.SupplierId, "Vendor", r.Reference, r.Reason, actor, DateTime.UtcNow, 6,
            [new(1, 1, "Rice", "kg", 2, 3, 6)]));
    public Task<IReadOnlyList<PurchaseReceiptDto>> ReceiptsAsync(int? beforeId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<PurchaseReceiptDto> ReceiptAsync(int id, CancellationToken ct) => throw new NotSupportedException();
}
