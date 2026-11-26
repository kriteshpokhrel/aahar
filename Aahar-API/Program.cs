using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Aahar.Application;
using Aahar.Domain;
using Aahar.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<RestaurantDbContext>((services, options) => options.UseNpgsql(
    services.GetRequiredService<IConfiguration>().GetConnectionString("Aahar")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Aahar to the dedicated PostgreSQL database connection string.")));
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<IGuestRequestService, GuestRequestService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
var businessZoneId = builder.Configuration["Business:TimeZone"];
if (string.IsNullOrWhiteSpace(businessZoneId)) throw new InvalidOperationException("Set Business:TimeZone to the restaurant's time zone.");
try
{
    builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(businessZoneId));
}
catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
{
    throw new InvalidOperationException("Business:TimeZone must identify a valid system time zone.", ex);
}
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "__Host-aahar-auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "__Host-aahar-csrf";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.Path = "/";
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("login", x => { x.PermitLimit = 5; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; });
    o.AddFixedWindowLimiter("guest-submit", x => { x.PermitLimit = 5; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; });
    o.AddFixedWindowLimiter("guest-status", x => { x.PermitLimit = 60; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; });
});

var app = builder.Build();
if (args.Contains("--bootstrap-admin"))
{
    var username = Environment.GetEnvironmentVariable("AAHAR_ADMIN_USERNAME")?.Trim().ToLowerInvariant();
    var password = Environment.GetEnvironmentVariable("AAHAR_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || password is not { Length: >= 12 and <= 256 })
        throw new InvalidOperationException("Set AAHAR_ADMIN_USERNAME and a password of at least 12 characters in AAHAR_ADMIN_PASSWORD.");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094799698)");
    if (await db.Users.AnyAsync()) throw new InvalidOperationException("Bootstrap refused: users already exist.");
    var admin = new User { Username = username, Role = "Admin" };
    admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, password);
    db.Users.Add(admin); await db.SaveChangesAsync(); await tx.CommitAsync();
    Console.WriteLine("Initial administrator created.");
    return;
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("HTTPS is required.");
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (AntiforgeryValidationException)
    {
        await Results.Problem(statusCode: 400, title: "Invalid antiforgery token").ExecuteAsync(context);
    }
    catch (DomainException ex)
    {
        await Results.Problem(statusCode: 400, title: "Invalid request", detail: ex.Message).ExecuteAsync(context);
    }
    catch (ForbiddenException)
    {
        await Results.Problem(statusCode: 403, title: "Forbidden").ExecuteAsync(context);
    }
    catch (ConflictException ex)
    {
        await Results.Problem(statusCode: 409, title: "Conflict", detail: ex.Message).ExecuteAsync(context);
    }
    catch (DbUpdateConcurrencyException)
    {
        await Results.Problem(statusCode: 409, title: "Conflict", detail: "The record changed. Refresh and retry.").ExecuteAsync(context);
    }
    catch (Exception ex) when (ex is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "40001" or "40P01" } }
        or PostgresException { SqlState: "40001" or "40P01" })
    {
        await Results.Problem(statusCode: 409, title: "Conflict", detail: "The operation conflicted with another change or an existing record. Refresh and retry.").ExecuteAsync(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var limit = context.GetEndpoint()?.Metadata.GetMetadata<RequestBodyLimit>()?.Bytes;
    if (limit is not null)
    {
        if (context.Request.ContentLength > limit)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        context.Request.EnableBuffering(bufferThreshold: limit.Value + 1, bufferLimit: (long)limit.Value + 1);
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while (total <= limit && (read = await context.Request.Body.ReadAsync(
            buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit.Value + 1 - total)), context.RequestAborted)) != 0)
            total += read;
        if (total > limit)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        context.Request.Body.Position = 0;
    }
    await next();
});
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
        HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});

var auth = app.MapGroup("/api/auth");
auth.MapGet("/csrf", (HttpContext ctx, IAntiforgery antiforgery) =>
    Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(ctx).RequestToken }));
auth.MapPost("/login", async (LoginRequest request, IRestaurantService service, HttpContext ctx, CancellationToken ct) =>
{
    var user = await service.AuthenticateAsync(request, ct);
    if (user is null) return Results.Unauthorized();
    var claims = new[] { new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role) };
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(user);
}).RequireRateLimiting("login");
auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new UserDto(user.Identity!.Name!, user.FindFirstValue(ClaimTypes.Role)!))).RequireAuthorization();
auth.MapPost("/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();

app.MapGet("/api/guest/menu", (IRestaurantService s, CancellationToken ct) => s.MenuAsync(true, ct));
app.MapPost("/api/guest/requests", (SubmitGuestRequest r, IGuestRequestService s, CancellationToken ct) => s.SubmitAsync(r, ct))
    .RequireRateLimiting("guest-submit").WithMetadata(new RequestBodyLimit(65536));
app.MapGet("/api/guest/requests/{id:int}", async (int id, HttpContext ctx, IGuestRequestService s, CancellationToken ct) =>
{
    var status = await s.StatusAsync(id, ctx.Request.Headers["X-Guest-Request-Token"].ToString(), ct);
    return status is null ? Results.NotFound() : Results.Ok(status);
}).RequireRateLimiting("guest-status");
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/guest-requests", (string? status, int? beforeId, int? limit, IGuestRequestService s, CancellationToken ct) => s.ListAsync(status, beforeId, limit ?? 50, ct))
    .RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/guest-requests/{id:int}/accept", (int id, ClaimsPrincipal user, IGuestRequestService s, CancellationToken ct) => s.AcceptAsync(id, user.Identity!.Name!, ct))
    .RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/guest-requests/{id:int}/reject", (int id, RejectGuestRequest r, ClaimsPrincipal user, IGuestRequestService s, CancellationToken ct) => s.RejectAsync(id, r, user.Identity!.Name!, ct))
    .RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/users", (CreateUserRequest r, IRestaurantService s, CancellationToken ct) => s.CreateUserAsync(r, ct)).RequireAuthorization(p => p.RequireRole("Admin"));
api.MapGet("/menu", (IRestaurantService s, CancellationToken ct) => s.MenuAsync(false, ct));
api.MapPost("/menu", (MenuRequest r, IRestaurantService s, CancellationToken ct) => s.SaveMenuAsync(null, r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPut("/menu/{id:int}", (int id, MenuRequest r, IRestaurantService s, CancellationToken ct) => s.SaveMenuAsync(id, r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/tables", (IRestaurantService s, CancellationToken ct) => s.TablesAsync(ct));
api.MapPost("/tables", (TableRequest r, IRestaurantService s, CancellationToken ct) => s.CreateTableAsync(r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/ingredients", (IRestaurantService s, CancellationToken ct) => s.IngredientsAsync(ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/ingredients/low-stock", (IInventoryService s, CancellationToken ct) => s.LowStockAsync(ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPost("/ingredients", (IngredientRequest r, ClaimsPrincipal user, IRestaurantService s, CancellationToken ct) => s.CreateIngredientAsync(r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPost("/ingredients/{id:int}/stock", (int id, StockRequest r, ClaimsPrincipal user, IRestaurantService s, CancellationToken ct) => s.AddStockAsync(id, r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPut("/ingredients/{id:int}/threshold", (int id, ThresholdRequest r, ClaimsPrincipal user, IInventoryService s, CancellationToken ct) => s.ChangeThresholdAsync(id, r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/ingredients/{id:int}/threshold-history", (int id, long? beforeId, int? limit, IInventoryService s, CancellationToken ct) => s.ThresholdHistoryAsync(id, beforeId, limit ?? 50, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPost("/ingredients/{id:int}/waste", (int id, WasteRequest r, ClaimsPrincipal user, IInventoryService s, CancellationToken ct) => s.WasteAsync(id, r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/ingredients/{id:int}/movements", (int id, long? beforeId, int? limit, IRestaurantService s, CancellationToken ct) => s.StockMovementsAsync(id, beforeId, limit ?? 50, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/suppliers", (IInventoryService s, CancellationToken ct) => s.SuppliersAsync(ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPost("/suppliers", (SupplierRequest r, ClaimsPrincipal user, IInventoryService s, CancellationToken ct) => s.SaveSupplierAsync(null, r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPut("/suppliers/{id:int}", (int id, SupplierRequest r, ClaimsPrincipal user, IInventoryService s, CancellationToken ct) => s.SaveSupplierAsync(id, r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/suppliers/{id:int}/history", (int id, long? beforeId, int? limit, IInventoryService s, CancellationToken ct) => s.SupplierHistoryAsync(id, beforeId, limit ?? 50, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPost("/purchase-receipts", (PurchaseReceiptRequest r, ClaimsPrincipal user, IInventoryService s, CancellationToken ct) => s.ReceiveAsync(r, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/purchase-receipts", (int? beforeId, int? limit, IInventoryService s, CancellationToken ct) => s.ReceiptsAsync(beforeId, limit ?? 50, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/purchase-receipts/{id:int}", (int id, IInventoryService s, CancellationToken ct) => s.ReceiptAsync(id, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/menu/{id:int}/recipe", (int id, IRestaurantService s, CancellationToken ct) => s.RecipeAsync(id, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapPut("/menu/{id:int}/recipe", async (int id, RecipeRequest r, IRestaurantService s, CancellationToken ct) => { await s.SetRecipeAsync(id, r, ct); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/orders", (IRestaurantService s, CancellationToken ct) => s.OrdersAsync(ct));
api.MapPost("/orders", (CreateOrderRequest r, IRestaurantService s, CancellationToken ct) => s.CreateOrderAsync(r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPut("/orders/{id:int}/items", (int id, EditOrderItemsRequest r, IRestaurantService s, CancellationToken ct) => s.EditOrderItemsAsync(id, r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/orders/{id:int}/send", (int id, ClaimsPrincipal user, IRestaurantService s, CancellationToken ct) => s.SendOrderAsync(id, user.Identity!.Name!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/orders/{id:int}/complete", (int id, IRestaurantService s, CancellationToken ct) => s.CompleteOrderAsync(id, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Kitchen"));
api.MapPost("/orders/{id:int}/cancel", (int id, CancelOrderRequest r, ClaimsPrincipal user, IRestaurantService s, CancellationToken ct) => s.CancelOrderAsync(id, r, user.Identity!.Name!, user.FindFirstValue(ClaimTypes.Role)!, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapPost("/orders/{id:int}/payments", (int id, PaymentRequest r, IRestaurantService s, CancellationToken ct) => s.PayAsync(id, r, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Staff"));
api.MapGet("/kitchen/tickets", (IRestaurantService s, CancellationToken ct) => s.KitchenTicketsAsync(ct));
api.MapPost("/kitchen/tickets/{id:int}/prepare", (int id, IRestaurantService s, CancellationToken ct) => s.PrepareTicketAsync(id, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Kitchen"));
api.MapPost("/kitchen/tickets/{id:int}/ready", (int id, IRestaurantService s, CancellationToken ct) => s.ReadyTicketAsync(id, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager", "Kitchen"));
api.MapGet("/sales", (IRestaurantService s, CancellationToken ct) => s.SalesAsync(ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/reports/sales", (string? fromDate, string? toDate, IReportService s, CancellationToken ct) => s.PaidSalesAsync(BusinessDateRange.Parse(fromDate), BusinessDateRange.Parse(toDate), ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
api.MapGet("/reports/best-selling", (string? fromDate, string? toDate, int? limit, IReportService s, CancellationToken ct) => s.BestSellingAsync(BusinessDateRange.Parse(fromDate), BusinessDateRange.Parse(toDate), limit ?? 20, ct)).RequireAuthorization(p => p.RequireRole("Admin", "Manager"));
app.Run();

public partial class Program;
public sealed record RequestBodyLimit(int Bytes);
