using Aahar_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Aahar_API.Data
{
    /// <summary>
    /// DB Context for Aahar.
    /// </summary>
    public class AaharDbContext: DbContext
    {
        /// <summary>
        /// Aahar DB Context constructor.
        /// </summary>
        /// <param name="options"></param>
        public AaharDbContext(DbContextOptions<AaharDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Represents DB set for <see cref="User"/> table.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Represents DB set for <see cref="Table"/> table.
        /// </summary>
        public DbSet<Table> DiningTables { get; set; }

        /// <summary>
        /// Represents DB set for <see cref="MenuItem"/> table.
        /// </summary>
        public DbSet<MenuItem> MenuItems { get; set; }

        /// <summary>
        /// Represents DB set for <see cref="Order"/> table.
        /// </summary>
        public DbSet<Order> Orders { get; set; }

        /// <summary>
        /// Represents DB set for <see cref="OrderItem"/> table.
        /// </summary>
        public DbSet<OrderItem> OrderItems { get; set; }

        /// <inheritdoc/>
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
