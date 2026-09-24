namespace EventAPI.Common
{
    /// <summary>
    /// The concert lifecycle:
    ///
    ///     Draft ──submit──► Pending ──approve──► Published
    ///                          │
    ///                          └──reject──► Rejected ──edit──► Rejected ──resubmit──► Pending
    ///
    /// Published/Pending can additionally be Cancelled.
    ///
    /// NOTE: the legacy code used "Approved" for the final published state. The
    /// lifecycle now uses "Published"; <see cref="Normalize"/> maps any legacy
    /// "Approved" rows so old data keeps working without a data migration.
    /// </summary>
    public static class EventStatus
    {
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Published = "Published";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        /// <summary>Legacy value kept only so existing rows can be normalized.</summary>
        public const string LegacyApproved = "Approved";

        /// <summary>Statuses whose content the owner is allowed to edit.</summary>
        public static readonly string[] Editable = { Draft, Rejected };

        /// <summary>Statuses that may be submitted for admin approval.</summary>
        public static readonly string[] Submittable = { Draft, Rejected };

        public static readonly string[] All =
            { Draft, Pending, Published, Rejected, Cancelled };

        /// <summary>Maps the legacy "Approved" value onto "Published".</summary>
        public static string Normalize(string? status) =>
            string.Equals(status, LegacyApproved, StringComparison.OrdinalIgnoreCase)
                ? Published
                : (status ?? Draft);

        /// <summary>True when the status is publicly visible / bookable.</summary>
        public static bool IsPublic(string? status) =>
            Normalize(status) == Published;

        public static bool IsValid(string? status) =>
            status != null && All.Contains(Normalize(status));
    }
}
