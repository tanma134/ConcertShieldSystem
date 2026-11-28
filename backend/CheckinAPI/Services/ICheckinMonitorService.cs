using System.Threading.Tasks;
using CheckinAPI.DTOs;

namespace CheckinAPI.Services
{
    public interface ICheckinMonitorService
    {
        Task<CheckinMonitorResponseDTO> GetMonitorStatusAsync(int eventId, string? gateId, string? zone, int currentUserId, bool isAdmin, string callerRole = "Organizer", bool strictValidation = false);
        Task<ScanResultResponseDTO> ScanQrTicketAsync(ScanQrRequestDTO request, int staffUserId, string staffUserName, string callerRole = "Staff", bool strictValidation = false);
        Task<ScanResultResponseDTO> ManualCheckinAsync(ManualCheckinRequestDTO request, int staffUserId, string staffUserName, string callerRole = "Staff", bool strictValidation = false);
        Task<LookupTicketResponseDTO> LookupTicketsAsync(LookupTicketRequestDTO request, int staffUserId, string callerRole = "Staff", bool strictValidation = false);
        Task<ScanHistoryResponseDTO> GetScanHistoryAsync(int eventId, string? gateId, string? statusFilter, int staffUserId, int limit = 20, string callerRole = "Staff", bool strictValidation = false);
        Task<GateAssignmentResponseDTO> GetGateAssignmentAsync(int eventId, int staffUserId, string callerRole = "Staff", bool strictValidation = false);
    }
}
