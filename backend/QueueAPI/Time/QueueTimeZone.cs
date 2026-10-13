namespace QueueAPI.Time;

/// <summary>
/// QueueAPI uses Vietnam local time for business timestamps. PostgreSQL columns
/// are timestamp without time zone, so their values are explicitly Vietnam wall-clock values.
/// </summary>
public static class QueueTimeZone
{
    private static readonly TimeZoneInfo Vietnam = ResolveVietnamTimeZone();

    public static DateTime Now => DateTime.SpecifyKind(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Vietnam), DateTimeKind.Unspecified);

    public static DateTime ToVietnam(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, Vietnam), DateTimeKind.Unspecified);

        // Unspecified input is a Vietnam wall-clock value, regardless of server timezone.
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    public static DateTime FromDatabase(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    public static DateTimeOffset AsVietnamOffset(DateTime value) =>
        new(FromDatabase(value), Vietnam.GetUtcOffset(value));

    public static DateTimeOffset NowOffset => AsVietnamOffset(Now);

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
