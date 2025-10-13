using Aahar_API.Data;
using Aahar_API.Data.UnitOfWork;
using Aahar_API.Repository.Implementations;
using Aahar_API.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AaharDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnectionString")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>()
    .AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>))
    .AddScoped<IMenuItemRepository, MenuItemRepository>()
    .AddScoped<IOrderItemRepository, OrderItemRepository>()
    .AddScoped<IOrderRepository, OrderRepository>()
    .AddScoped<IUserRepository, UserRepository>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
