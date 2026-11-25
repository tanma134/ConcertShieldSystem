using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckinAPI.API
{
    public class EventInfoResult
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string ReEntryPolicy { get; set; } = "NoReEntry";
        public int? ReEntryDurationMinutes { get; set; }
    }

    public class EventGateInfo
    {
        public int GateId { get; set; }
        public int EventId { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? AllowedTicketTypes { get; set; }
        public List<string> AllowedTicketTypeList { get; set; } = new();
    }

    public class StaffAssignmentInfo
    {
        public int EventStaffId { get; set; }
        public int StaffUserId { get; set; }
        public int EventId { get; set; }
        public string? GateName { get; set; }
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
        public string? ShiftDate { get; set; }
        public string? EventTitle { get; set; }
        public bool CanCheckin { get; set; } = true;
    }

    public interface IEventApiClient
    {
        Task<EventInfoResult?> GetEventInfoAsync(int eventId);
        Task<List<EventGateInfo>> GetEventGatesAsync(int eventId);
        Task<StaffAssignmentInfo?> GetStaffAssignmentAsync(int eventId, int staffUserId);
    }
}
