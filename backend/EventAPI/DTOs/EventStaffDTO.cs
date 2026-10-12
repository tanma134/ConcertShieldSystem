namespace EventAPI.DTOs
{
    // Body of POST api/events/{id}/staff.
    public class AssignStaffDTO
    {
        public int StaffUserId { get; set; }
        public string GateName { get; set; } = "Main Gate";
        public bool CanReviewReturns { get; set; }
    }

    // Body of PUT api/events/{id}/staff/{staffUserId}/return-review.
    public class SetReturnReviewDTO
    {
        public bool CanReviewReturns { get; set; }
    }

    // One staff member assigned to an event (UC_14.3).
    public class EventStaffDTO
    {
        public int EventStaffId { get; set; }
        public int EventId { get; set; }
        public int StaffUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GateName { get; set; } = "Main Gate";
        public bool CanReviewReturns { get; set; }
        public DateTime AssignedAt { get; set; }
        public int AssignedBy { get; set; }
    }

    // One Staff account the organizer can pick from (UC_14.4).
    public class StaffCandidateDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }
}
