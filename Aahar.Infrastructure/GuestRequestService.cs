using System.Data;
using Aahar.Application;
using Aahar.Domain;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Aahar.Infrastructure;

public sealed class GuestRequestService(RestaurantDbContext db) : IGuestRequestService
{
    private static GuestRequestDto ToDto(GuestRequest request) =>
        new(request.Id, request.TableId, request.Status, request.CreatedAt, request.DecidedAt, request.DecidedBy,
            request.RejectionReason, request.OrderId, request.Items.Adapt<List<GuestRequestItemDto>>());

    private static List<GuestQuoteLine> Lines(List<GuestRequestLine>? items)
    {
        if (items is not { Count: > 0 and <= 100 } || items.Any(x => x is null))
            throw new DomainException("Invalid guest request items.");
        return items.Select(x => new GuestQuoteLine(x.MenuItemId, x.Quantity, x.QuotedUnitPrice, x.Notes)).ToList();
    }

    private async Task<Dictionary<int, MenuItem>> LockedMenuAsync(IEnumerable<int> menuIds, CancellationToken ct)
    {
        var ids = menuIds.Distinct().Order().ToArray();
        var items = await db.MenuItems.FromSqlInterpolated(
            $"SELECT * FROM \"MenuItems\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR SHARE").ToListAsync(ct);
        return items.ToDictionary(x => x.Id);
    }

    public async Task<GuestSubmissionDto> SubmitAsync(SubmitGuestRequest r, CancellationToken ct)
    {
        if (r.TableId is not null and <= 0) throw new DomainException("Invalid table.");
        var lines = Lines(r.Items);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (r.TableId is not null && !await db.Tables.AnyAsync(x => x.Id == r.TableId, ct))
            throw new DomainException("Table not found.");
        var menu = await LockedMenuAsync(lines.Select(x => x.MenuItemId), ct);
        var items = GuestRequestRules.ValidateQuote(lines, menu);
        var token = GuestAccessToken.Create();
        var request = new GuestRequest { TableId = r.TableId, Items = items, AccessTokenHash = GuestAccessToken.Hash(token) };
        db.GuestRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new GuestSubmissionDto(request.Id, request.Status, token);
    }

    public async Task<GuestStatusDto?> StatusAsync(int id, string? token, CancellationToken ct)
    {
        if (id <= 0 || !GuestAccessToken.IsValidFormat(token)) return null;
        var request = await db.GuestRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null || !GuestAccessToken.Matches(token!, request.AccessTokenHash)) return null;
        return new GuestStatusDto(request.Id, request.Status, request.OrderId);
    }

    public async Task<IReadOnlyList<GuestRequestDto>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100 || beforeId is <= 0 || status is not (null or "Pending" or "Accepted" or "Rejected"))
            throw new DomainException("Invalid request filter or page.");
        return (await db.GuestRequests.AsNoTracking().Include(x => x.Items)
            .Where(x => (beforeId == null || x.Id < beforeId) && (status == null || x.Status == status))
            .OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<GuestAcceptanceDto> AcceptAsync(int id, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.GuestRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Guest request not found.");
        if (request.Status != "Pending") throw new ConflictException("This request has already been decided.");
        if (request.TableId is int tableId && await db.Orders.AnyAsync(x => x.TableId == tableId &&
            (x.Status == "Draft" || x.Status == "Sent" || x.Status == "Completed"), ct))
            throw new ConflictException("The selected table is occupied.");
        var lines = request.Items.Select(x => new GuestQuoteLine(x.MenuItemId, x.Quantity, x.QuotedUnitPrice, x.Notes)).ToList();
        var menu = await LockedMenuAsync(lines.Select(x => x.MenuItemId), ct);
        GuestRequestRules.ValidateQuote(lines, menu);
        var order = new Order { TableId = request.TableId };
        OrderRules.SetDraftItems(order, lines.Select(x => new DraftLine(x.MenuItemId, x.Quantity, x.Notes)).ToList(), menu);
        GuestRequestRules.Accept(request, order, actor);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new GuestAcceptanceDto(ToDto(request), RestaurantService.ToDto(order));
    }

    public async Task<GuestRequestDto> RejectAsync(int id, RejectGuestRequest r, string actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.GuestRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("Guest request not found.");
        GuestRequestRules.Reject(request, r.Reason, actor);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(request);
    }
}
