namespace EventAPI.Models
{
    // A Staff account assigned to work (scan tickets) at one event.
    public class EventStaff
    {
        public int EventStaffId { get; set; }

        public int EventId { get; set; }

        public int StaffUserId { get; set; }

        public string GateName { get; set; } = "Main Gate";

        // True when the organizer let this staff member review ticket return requests of the event.
        public bool CanReviewReturns { get; set; }

        public int AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int? UnassignedBy { get; set; }

        public DateTime? UnassignedAt { get; set; }
    }
}
