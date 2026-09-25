namespace EventAPI.Common
{

    // How a zone is sold. A single concert may mix both — numbered seats upstairs,
    // a standing pit downstairs.

    public static class SeatZoneType
    {
        // Numbered seats. One Seat row per physical seat; buyers choose their seat.
        public const string Seated = "Seated";

        // Standing / general-admission area. No Seat rows, just a headcount.
        public const string Standing = "Standing";

        public static readonly string[] All = { Seated, Standing };

        public static bool IsValid(string? value) =>
            value != null && All.Contains(Normalize(value));

        // Case-insensitive normalisation so "seated"/"SEATED" both work.
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
