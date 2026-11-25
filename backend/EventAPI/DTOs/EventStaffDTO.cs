namespace EventAPI.DTOs
{
    // Body of POST api/events/{id}/staff.
    public class AssignStaffDTO
    {
        public int StaffUserId { get; set; }
        public string GateName { get; set; } = "Main Gate";
        public bool CanCheckin { get; set; } = true;
        public bool CanReviewReturns { get; set; }
        public bool CanManageIncidents { get; set; }
        public bool CanManageSupportCases { get; set; }
        public string? ShiftDate { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
    }

    // Body of PUT api/events/{id}/staff/{staffUserId}/shift.
    public class UpdateStaffShiftDTO
    {
        public string? GateName { get; set; }
        public string? ShiftDate { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
    }

    // Body of PUT api/events/{id}/staff/{staffUserId}/return-review.
    public class SetReturnReviewDTO
    {
        public bool CanReviewReturns { get; set; }
    }

    // Body of PUT api/events/{id}/staff/{staffUserId}/duties.
    public class UpdateStaffDutiesDTO
    {
        public bool? CanCheckin { get; set; }
        public bool? CanReviewReturns { get; set; }
        public bool? CanManageIncidents { get; set; }
        public bool? CanManageSupportCases { get; set; }
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
        public string? ShiftDate { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
        public bool CanCheckin { get; set; } = true;
        public bool CanReviewReturns { get; set; }
        public bool CanManageIncidents { get; set; }
        public bool CanManageSupportCases { get; set; }
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

    // An event and gate a staff member is assigned to.
    public class StaffEventAssignmentDTO
    {
        public int EventStaffId { get; set; }
        public int EventId { get; set; }
        public int StaffUserId { get; set; }
        public string GateName { get; set; } = "Main Gate";
        public string? ShiftDate { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CanCheckin { get; set; } = true;
        public bool CanReviewReturns { get; set; }
        public bool CanManageIncidents { get; set; }
        public bool CanManageSupportCases { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
