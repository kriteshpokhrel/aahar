using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aahar.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Aahar")
            ?? "Host=localhost;Database=design_time_only;Username=unused;Password=unused";
        return new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>().UseNpgsql(connection).Options);
    }
}
