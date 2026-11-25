using Aahar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> Recipes => Set<RecipeIngredient>();
    public DbSet<Order> Orders => Set<Order>();
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
        b.Entity<RecipeIngredient>().HasKey(x => new { x.MenuItemId, x.IngredientId });
        b.Entity<RecipeIngredient>().Property(x => x.Quantity).HasPrecision(14, 3);
        b.Entity<RecipeIngredient>().ToTable(t => t.HasCheckConstraint("CK_Recipes_Quantity", "\"Quantity\" > 0"));
        b.Entity<RecipeIngredient>().HasOne(x => x.MenuItem).WithMany(x => x.Recipe).HasForeignKey(x => x.MenuItemId);
        b.Entity<RecipeIngredient>().HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Order>().HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OrderItem>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Entity<OrderItem>().HasIndex(x => new { x.OrderId, x.MenuItemId }).IsUnique();
        b.Entity<OrderItem>().ToTable(t => t.HasCheckConstraint("CK_OrderItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        b.Entity<OrderItem>().HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Payment>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<Payment>().Property(x => x.Amount).HasPrecision(14, 2);
        b.Entity<Payment>().Property(x => x.Method).HasMaxLength(20);
        b.Entity<Payment>().HasOne(x => x.Order).WithOne(x => x.Payment).HasForeignKey<Payment>(x => x.OrderId);
    }
}
