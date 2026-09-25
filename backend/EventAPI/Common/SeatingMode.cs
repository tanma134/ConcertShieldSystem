namespace EventAPI.Common
{

    // The 3 possible seating modes for a concert, derived automatically from its
    // zones (see SeatingService) — organizers never set this directly:
    // 1. GeneralAdmission: No seating chart. Capacity is taken directly from TicketType.Quantity.
    // 2. StandingZones: Zone map exists and every zone is headcount-only (no Seat rows).
    // 3. ReservedSeating: Zone map exists and at least one zone has individual numbered seats.
    //    A chart may freely mix Seated and Standing zones (e.g. numbered seats on the
    //    balcony plus a standing pit at the front) — as soon as any zone is Seated,
    //    the event as a whole is classified ReservedSeating.

    public static class SeatingMode
    {
        public const string GeneralAdmission = "GeneralAdmission";
        public const string StandingZones = "StandingZones";
        public const string ReservedSeating = "ReservedSeating";

        public static readonly string[] All = { GeneralAdmission, StandingZones, ReservedSeating };

        public static bool IsValid(string? value) =>
            value != null && All.Contains(Normalize(value));

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ReservedSeating;

            return All.FirstOrDefault(m => string.Equals(m, value.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? value.Trim();
        }

        public static bool IsGeneralAdmission(string? value) =>
            Normalize(value) == GeneralAdmission;

        public static bool IsStandingZones(string? value) =>
            Normalize(value) == StandingZones;

        public static bool IsReservedSeating(string? value) =>
            Normalize(value) == ReservedSeating;
    }
}
