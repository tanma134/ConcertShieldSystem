namespace EventAPI.Common
{
    /// <summary>
    /// The 3 exclusive seating modes for a concert.
    /// An event must strictly use one of these three modes:
    /// 1. GeneralAdmission: No seating chart. Capacity is taken directly from TicketType.Quantity.
    /// 2. StandingZones: Zone map exists. Each zone has capacity/headcount only (no Seat rows).
    /// 3. ReservedSeating: Seat map exists with rows and individual seats per zone.
    /// Mixing Standing and Seated within the same event is strictly prohibited.
    /// </summary>
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
