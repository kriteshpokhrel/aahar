using System.Globalization;

namespace Aahar.Domain;

public sealed record BusinessDateRange(DateOnly FromDate, DateOnly ToDate, string TimeZone, DateTime FromUtc, DateTime ToUtcExclusive)
{
    public static DateOnly? Parse(string? value)
    {
        if (value is null) return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new DomainException("Dates must use YYYY-MM-DD.");
        return date;
    }

    public static BusinessDateRange Create(DateOnly? from, DateOnly? to, TimeZoneInfo zone)
    {
        if (from is null || to is null || from.Value < new DateOnly(1900, 1, 1) ||
            to.Value > new DateOnly(9998, 12, 30) || from.Value > to.Value ||
            to.Value.DayNumber - from.Value.DayNumber >= 366)
            throw new DomainException("Provide an inclusive date range of at most 366 days, between 1900-01-01 and 9998-12-30.");

        var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(start) || zone.IsAmbiguousTime(start) || zone.IsInvalidTime(end) || zone.IsAmbiguousTime(end))
            throw new DomainException("A business-day boundary is ambiguous or invalid in the configured time zone.");
        return new BusinessDateRange(from.Value, to.Value, zone.Id,
            TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone));
    }
}
