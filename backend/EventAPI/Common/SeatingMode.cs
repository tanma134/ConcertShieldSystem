namespace EventAPI.Common
{
    // Exclusive seating modes for a concert; mixing standing and seated within one event is prohibited.
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
