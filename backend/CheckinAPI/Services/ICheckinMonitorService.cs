using System.Threading.Tasks;
using CheckinAPI.DTOs;

namespace CheckinAPI.Services
{
    public interface ICheckinMonitorService
    {
        Task<CheckinMonitorResponseDTO> GetMonitorStatusAsync(int eventId, string? gateId, string? zone, int currentUserId, bool isAdmin);
        Task<ScanResultResponseDTO> ScanQrTicketAsync(ScanQrRequestDTO request, int staffUserId, string staffUserName);
        Task<ScanResultResponseDTO> ManualCheckinAsync(ManualCheckinRequestDTO request, int staffUserId, string staffUserName);
        Task<LookupTicketResponseDTO> LookupTicketsAsync(LookupTicketRequestDTO request, int staffUserId);
        Task<ScanHistoryResponseDTO> GetScanHistoryAsync(int eventId, string? gateId, string? statusFilter, int staffUserId);
        Task<GateAssignmentResponseDTO> GetGateAssignmentAsync(int eventId, int staffUserId);
    }
}
