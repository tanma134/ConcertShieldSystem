using System;

namespace EventAPI.Common
{
    /// <summary>
    /// Npgsql maps DateTime to PostgreSQL "timestamp with time zone", which only accepts
    /// DateTimeKind.Utc. A JSON body such as "2027-06-01T19:00:00" (no offset) deserializes
    /// to DateTimeKind.Unspecified and used to make SaveChanges throw, surfacing as a 500.
    /// Everything coming from a client is therefore converted here:
    ///   - Utc      -> unchanged
    ///   - Local    -> ToUniversalTime()
    ///   - Unspecified -> interpreted in the event timezone (default SE Asia), then converted
    /// </summary>
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
