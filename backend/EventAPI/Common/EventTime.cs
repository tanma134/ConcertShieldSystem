using System;

namespace EventAPI.Common
{
    // Npgsql maps DateTime to PostgreSQL "timestamp with time zone", which only accepts DateTimeKind.Utc.
    // Unspecified client timestamps are interpreted in the event timezone (default SE Asia) and converted to UTC.
    public static class EventTime
    {
        public const string DefaultTimezone = "SE Asia Standard Time";

        public static DateTime ToUtc(DateTime value, string? timezone = null)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
                        ResolveTimezone(timezone))
            };
        }

        public static DateTime? ToUtc(DateTime? value, string? timezone = null)
            => value.HasValue ? ToUtc(value.Value, timezone) : null;

        private static TimeZoneInfo ResolveTimezone(string? timezone)
        {
            var id = string.IsNullOrWhiteSpace(timezone) ? DefaultTimezone : timezone;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception)
            {
                // Unknown id (or a Linux/Windows name mismatch): fall back to the default.
                return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
            }
        }
    }
}
