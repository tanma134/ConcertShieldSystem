namespace EventAPI.Models
{
    // A Staff account assigned to work (scan tickets) at one event.
    public class EventStaff
    {
        public int EventStaffId { get; set; }

        public int EventId { get; set; }

        public int StaffUserId { get; set; }

        public string GateName { get; set; } = "Main Gate";

        // Shift date & hours (e.g. "2026-11-18", "08:00", "17:00")
        public DateOnly? ShiftDate { get; set; }
        public DateOnly? ShiftEndDate { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }

        // True when the organizer lets this staff member scan tickets / gate check-in.
        public bool CanCheckin { get; set; } = true;

        // True when the organizer let this staff member review ticket return requests of the event.
        public bool CanReviewReturns { get; set; }

        // True when the organizer lets this staff member report and manage incidents at the event.
        public bool CanManageIncidents { get; set; }

        // True when the organizer lets this staff member handle customer support cases / helpdesk.
        public bool CanManageSupportCases { get; set; }

        public int AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int? UnassignedBy { get; set; }

        public DateTime? UnassignedAt { get; set; }
    }
}
