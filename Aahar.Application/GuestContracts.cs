namespace Aahar.Application;

public record GuestRequestLine(int MenuItemId, int Quantity, decimal? QuotedUnitPrice, string? Notes = null);
public record SubmitGuestRequest(int? TableId, List<GuestRequestLine> Items);
public record GuestSubmissionDto(int Id, string Status, string AccessToken);
public record GuestStatusDto(int Id, string Status, int? OrderId);
public record GuestRequestItemDto(int MenuItemId, string Name, int Quantity, decimal QuotedUnitPrice, string? Notes);
public record GuestRequestDto(int Id, int? TableId, string Status, DateTime CreatedAt, DateTime? DecidedAt,
    string? DecidedBy, string? RejectionReason, int? OrderId, List<GuestRequestItemDto> Items);
public record RejectGuestRequest(string Reason);
public record GuestAcceptanceDto(GuestRequestDto Request, OrderDto Order);

public interface IGuestRequestService
{
    Task<GuestSubmissionDto> SubmitAsync(SubmitGuestRequest request, CancellationToken ct);
    Task<GuestStatusDto?> StatusAsync(int id, string? token, CancellationToken ct);
    Task<IReadOnlyList<GuestRequestDto>> ListAsync(string? status, int? beforeId, int limit, CancellationToken ct);
    Task<GuestAcceptanceDto> AcceptAsync(int id, string actor, CancellationToken ct);
    Task<GuestRequestDto> RejectAsync(int id, RejectGuestRequest request, string actor, CancellationToken ct);
}
