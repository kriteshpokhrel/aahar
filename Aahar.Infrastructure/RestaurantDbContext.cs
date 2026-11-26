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
    public DbSet<Payment> Payments => Set<Payment>();

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
        b.Entity<Ingredient>().ToTable(t => t.HasCheckConstraint("CK_Ingredients_Stock", "\"Stock\" >= 0"));
        b.Entity<StockMovement>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<StockMovement>().Property(x => x.Actor).HasMaxLength(100);
        b.Entity<StockMovement>().Property(x => x.QuantityChange).HasPrecision(14, 3);
        b.Entity<StockMovement>().Property(x => x.BalanceAfter).HasPrecision(14, 3);
        b.Entity<StockMovement>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasIndex(x => new { x.OrderId, x.IngredientId, x.Kind }).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        b.Entity<StockMovement>().HasIndex(x => new { x.IngredientId, x.Id });
        b.Entity<StockMovement>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Balance", "\"BalanceAfter\" >= 0");
            t.HasCheckConstraint("CK_StockMovements_Kind", "(\"Kind\" = 'Opening' AND \"QuantityChange\" >= 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Receipt' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Consumption' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Cancellation' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NOT NULL)");
        });
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
        b.Entity<OrderItem>().HasIndex(x => new { x.OrderId, x.MenuItemId }).IsUnique();
        b.Entity<OrderItem>().ToTable(t => t.HasCheckConstraint("CK_OrderItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        b.Entity<OrderItem>().HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicket>().HasOne(x => x.Order).WithOne(x => x.Ticket).HasForeignKey<KitchenTicket>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicket>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<KitchenTicket>().ToTable(t => t.HasCheckConstraint("CK_KitchenTickets_Status", "\"Status\" IN ('Queued', 'Preparing', 'Ready', 'Cancelled')"));
        b.Entity<KitchenTicketItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<KitchenTicketItem>().Property(x => x.Notes).HasMaxLength(300);
        b.Entity<KitchenTicketItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<KitchenTicketItem>().HasOne(x => x.Ticket).WithMany(x => x.Items).HasForeignKey(x => x.KitchenTicketId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KitchenTicketItem>().HasIndex(x => new { x.KitchenTicketId, x.MenuItemId }).IsUnique();
        b.Entity<KitchenTicketItem>().ToTable(t => t.HasCheckConstraint("CK_KitchenTicketItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        b.Entity<Payment>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<Payment>().Property(x => x.Amount).HasPrecision(14, 2);
        b.Entity<Payment>().Property(x => x.Method).HasMaxLength(20);
        b.Entity<Payment>().HasOne(x => x.Order).WithOne(x => x.Payment).HasForeignKey<Payment>(x => x.OrderId);
    }
}
