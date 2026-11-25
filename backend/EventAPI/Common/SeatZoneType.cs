namespace EventAPI.Common
{
    // A single concert may mix Seated (numbered seats) and Standing (headcount-only) zones.
    public static class SeatZoneType
    {
        public const string Seated = "Seated";

        public const string Standing = "Standing";

        public static readonly string[] All = { Seated, Standing };

        public static bool IsValid(string? value) =>
            value != null && All.Contains(Normalize(value));

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Seated;

            return All.FirstOrDefault(t => string.Equals(t, value.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? value.Trim();
        }

        public static bool IsSeated(string? value) => Normalize(value) == Seated;
        public static bool IsStanding(string? value) => Normalize(value) == Standing;
    }
}
