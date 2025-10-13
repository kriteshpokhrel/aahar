using Aahar_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Aahar_API.Data
{
    public class AaharDbContext: DbContext
    {
        public AaharDbContext(DbContextOptions<AaharDbContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Table> DiningTables { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Table>()
                .HasKey(t => t.Id);

            modelBuilder.Entity<Table>()
                .HasIndex(t => t.Number)
                .IsUnique();

            modelBuilder.Entity<MenuItem>()
                .HasKey(m => m.Id);

            modelBuilder.Entity<Order>()
                .HasKey(o => o.Id);
            
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Table)
                .WithMany()
                .HasForeignKey(o => o.TableId);

            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.Id);
            
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId);
            
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.MenuItem)
                .WithMany()
                .HasForeignKey(oi => oi.MenuItemId);
        }
    }
}
