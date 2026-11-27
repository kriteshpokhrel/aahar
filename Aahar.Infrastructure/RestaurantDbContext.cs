using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RecipeIngredient> Recipes => Set<RecipeIngredient>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<KitchenTicket> KitchenTickets => Set<KitchenTicket>();
    public DbSet<GuestRequest> GuestRequests => Set<GuestRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierChange> SupplierChanges => Set<SupplierChange>();
    public DbSet<IngredientThresholdChange> IngredientThresholdChanges => Set<IngredientThresholdChange>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<User>().Property(x => x.Username).HasMaxLength(100);
        b.Entity<User>().Property(x => x.Role).HasMaxLength(20);
        b.Entity<MenuItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<MenuItem>().Property(x => x.Description).HasMaxLength(300);
        b.Entity<MenuItem>().Property(x => x.Category).HasMaxLength(100);
        b.Entity<MenuItem>().Property(x => x.Price).HasPrecision(12, 2);
        b.Entity<MenuItem>().ToTable(t => t.HasCheckConstraint("CK_MenuItems_Price", "\"Price\" >= 0"));
        b.Entity<DiningTable>().HasIndex(x => x.Number).IsUnique();
        b.Entity<Ingredient>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Ingredient>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Ingredient>().Property(x => x.Unit).HasMaxLength(30);
        b.Entity<Ingredient>().Property(x => x.Stock).HasPrecision(14, 3);
        b.Entity<Ingredient>().Property(x => x.LowStockThreshold).HasPrecision(14, 3);
        b.Entity<Ingredient>().Ignore(x => x.IsLowStock);
        b.Entity<Ingredient>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Ingredients_Stock", "\"Stock\" >= 0");
            t.HasCheckConstraint("CK_Ingredients_Threshold", "\"LowStockThreshold\" >= 0");
        });
        b.Entity<StockMovement>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<StockMovement>().Property(x => x.Actor).HasMaxLength(100);
        b.Entity<StockMovement>().Property(x => x.Reason).HasMaxLength(300);
        b.Entity<StockMovement>().Property(x => x.QuantityChange).HasPrecision(14, 3);
        b.Entity<StockMovement>().Property(x => x.BalanceAfter).HasPrecision(14, 3);
        b.Entity<StockMovement>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne(x => x.PurchaseReceiptLine).WithOne().HasForeignKey<StockMovement>(x => x.PurchaseReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne(x => x.KitchenTicket).WithMany().HasForeignKey(x => x.KitchenTicketId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasIndex(x => new { x.OrderId, x.IngredientId, x.Kind }).IsUnique().HasFilter("\"OrderId\" IS NOT NULL AND \"KitchenTicketId\" IS NULL");
        b.Entity<StockMovement>().HasIndex(x => new { x.KitchenTicketId, x.IngredientId }).IsUnique().HasFilter("\"KitchenTicketId\" IS NOT NULL");
        b.Entity<StockMovement>().HasIndex(x => new { x.IngredientId, x.Id });
        b.Entity<StockMovement>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Balance", "\"BalanceAfter\" >= 0");
            t.HasCheckConstraint("CK_StockMovements_Kind", "((\"Kind\" = 'Opening' AND \"QuantityChange\" >= 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Receipt' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Consumption' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Cancellation' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Waste' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NULL)) AND \"PurchaseReceiptLineId\" IS NULL OR (\"Kind\" = 'Purchase' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL AND \"PurchaseReceiptLineId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_StockMovements_Reason", "\"Kind\" NOT IN ('Purchase', 'Waste') OR (\"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) > 0)");
            t.HasCheckConstraint("CK_StockMovements_Ticket", "\"KitchenTicketId\" IS NULL OR (\"Kind\" = 'Consumption' AND \"OrderId\" IS NOT NULL)");
        });
        b.Entity<Supplier>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Supplier>().Property(x => x.Contact).HasMaxLength(200);
        b.Entity<Supplier>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Supplier>().HasIndex(x => x.Name).IsUnique();
        b.Entity<SupplierChange>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<SupplierChange>().Property(x => x.Contact).HasMaxLength(200);
        b.Entity<SupplierChange>().Property(x => x.Actor).HasMaxLength(100);
        b.Entity<SupplierChange>().Property(x => x.Reason).HasMaxLength(300);
        b.Entity<SupplierChange>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SupplierChange>().HasIndex(x => new { x.SupplierId, x.Id });
        b.Entity<IngredientThresholdChange>().Property(x => x.PreviousThreshold).HasPrecision(14, 3);
        b.Entity<IngredientThresholdChange>().Property(x => x.NewThreshold).HasPrecision(14, 3);
        b.Entity<IngredientThresholdChange>().Property(x => x.Actor).HasMaxLength(100);
        b.Entity<IngredientThresholdChange>().Property(x => x.Reason).HasMaxLength(300);
        b.Entity<IngredientThresholdChange>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IngredientThresholdChange>().HasIndex(x => new { x.IngredientId, x.Id });
        b.Entity<IngredientThresholdChange>().ToTable(t => t.HasCheckConstraint("CK_ThresholdChanges_Values", "\"NewThreshold\" >= 0 AND (\"PreviousThreshold\" IS NULL OR \"PreviousThreshold\" >= 0)"));
        b.Entity<PurchaseReceipt>().Property(x => x.SupplierName).HasMaxLength(100);
        b.Entity<PurchaseReceipt>().Property(x => x.Reference).HasMaxLength(100);
        b.Entity<PurchaseReceipt>().Property(x => x.Reason).HasMaxLength(300);
        b.Entity<PurchaseReceipt>().Property(x => x.Actor).HasMaxLength(100);
        b.Entity<PurchaseReceipt>().Property(x => x.Total).HasPrecision(14, 2);
        b.Entity<PurchaseReceipt>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseReceipt>().HasIndex(x => new { x.SupplierId, x.Reference }).IsUnique();
        b.Entity<PurchaseReceipt>().ToTable(t => t.HasCheckConstraint("CK_PurchaseReceipts_Total", "\"Total\" >= 0"));
        b.Entity<PurchaseReceiptLine>().Property(x => x.IngredientName).HasMaxLength(100);
        b.Entity<PurchaseReceiptLine>().Property(x => x.Unit).HasMaxLength(30);
        b.Entity<PurchaseReceiptLine>().Property(x => x.Quantity).HasPrecision(14, 3);
        b.Entity<PurchaseReceiptLine>().Property(x => x.UnitCost).HasPrecision(12, 2);
        b.Entity<PurchaseReceiptLine>().Property(x => x.LineTotal).HasPrecision(14, 2);
        b.Entity<PurchaseReceiptLine>().HasOne(x => x.PurchaseReceipt).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseReceiptLine>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseReceiptLine>().HasIndex(x => new { x.PurchaseReceiptId, x.IngredientId }).IsUnique();
        b.Entity<PurchaseReceiptLine>().ToTable(t => t.HasCheckConstraint("CK_PurchaseReceiptLines_Values", "\"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"LineTotal\" >= 0"));
        b.Entity<RecipeIngredient>().HasKey(x => new { x.MenuItemId, x.IngredientId });
        b.Entity<RecipeIngredient>().Property(x => x.Quantity).HasPrecision(14, 3);
        b.Entity<RecipeIngredient>().ToTable(t => t.HasCheckConstraint("CK_Recipes_Quantity", "\"Quantity\" > 0"));
        b.Entity<RecipeIngredient>().HasOne(x => x.MenuItem).WithMany(x => x.Recipe).HasForeignKey(x => x.MenuItemId);
        b.Entity<RecipeIngredient>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Order>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Order>().Property(x => x.CancelledBy).HasMaxLength(100);
        b.Entity<Order>().Property(x => x.CancellationReason).HasMaxLength(300);
        b.Entity<Order>().Property(x => x.CancellationStockDisposition).HasMaxLength(20);
        b.Entity<Order>().HasIndex(x => x.TableId).IsUnique().HasFilter("\"TableId\" IS NOT NULL AND \"Status\" IN ('Draft', 'Sent', 'Completed')").HasDatabaseName("IX_Orders_ActiveTable");
        b.Entity<Order>().HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OrderItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<OrderItem>().Property(x => x.Notes).HasMaxLength(300);
        b.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<OrderItem>().HasIndex(x => new { x.OrderId, x.MenuItemId });
        b.Entity<OrderItem>().ToTable(t => t.HasCheckConstraint("CK_OrderItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        b.Entity<OrderItem>().HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicket>().HasOne(x => x.Order).WithMany(x => x.Tickets).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicket>().HasIndex(x => x.OrderId);
        b.Entity<KitchenTicket>().HasIndex(x => new { x.OrderId, x.RequestId }).IsUnique().HasFilter("\"RequestId\" IS NOT NULL");
        b.Entity<KitchenTicket>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<KitchenTicket>().ToTable(t => t.HasCheckConstraint("CK_KitchenTickets_Status", "\"Status\" IN ('Queued', 'Preparing', 'Ready', 'Cancelled')"));
        b.Entity<KitchenTicketItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<KitchenTicketItem>().Property(x => x.Notes).HasMaxLength(300);
        b.Entity<KitchenTicketItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<KitchenTicketItem>().HasOne(x => x.Ticket).WithMany(x => x.Items).HasForeignKey(x => x.KitchenTicketId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicketItem>().HasIndex(x => new { x.KitchenTicketId, x.MenuItemId }).IsUnique();
        b.Entity<KitchenTicketItem>().ToTable(t => t.HasCheckConstraint("CK_KitchenTicketItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        b.Entity<GuestRequest>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<GuestRequest>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<GuestRequest>().Property(x => x.DecidedBy).HasMaxLength(100);
        b.Entity<GuestRequest>().Property(x => x.RejectionReason).HasMaxLength(300);
        b.Entity<GuestRequest>().HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestRequest>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestRequest>().HasIndex(x => x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        b.Entity<GuestRequest>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_GuestRequests_Token", "octet_length(\"AccessTokenHash\") = 32");
            t.HasCheckConstraint("CK_GuestRequests_Decision", "(\"Status\" = 'Pending' AND \"DecidedAt\" IS NULL AND \"DecidedBy\" IS NULL AND \"OrderId\" IS NULL AND \"RejectionReason\" IS NULL) OR (\"Status\" = 'Accepted' AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL AND \"OrderId\" IS NOT NULL AND \"RejectionReason\" IS NULL) OR (\"Status\" = 'Rejected' AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL AND \"OrderId\" IS NULL AND \"RejectionReason\" IS NOT NULL)");
        });
        b.Entity<GuestRequestItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<GuestRequestItem>().Property(x => x.Notes).HasMaxLength(300);
        b.Entity<GuestRequestItem>().Property(x => x.QuotedUnitPrice).HasPrecision(12, 2);
        b.Entity<GuestRequestItem>().HasOne(x => x.Request).WithMany(x => x.Items).HasForeignKey(x => x.GuestRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestRequestItem>().HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestRequestItem>().HasIndex(x => new { x.GuestRequestId, x.MenuItemId }).IsUnique();
        b.Entity<GuestRequestItem>().ToTable(t => t.HasCheckConstraint("CK_GuestRequestItems_Values", "\"Quantity\" > 0 AND \"QuotedUnitPrice\" >= 0"));
        b.Entity<Payment>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<Payment>().Property(x => x.Amount).HasPrecision(14, 2);
        b.Entity<Payment>().Property(x => x.Method).HasMaxLength(20);
        b.Entity<Payment>().HasOne(x => x.Order).WithOne(x => x.Payment).HasForeignKey<Payment>(x => x.OrderId);
    }
}
