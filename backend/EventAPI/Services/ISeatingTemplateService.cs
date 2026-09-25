using EventAPI.DTOs;

namespace EventAPI.Services
{
    // UC_26.2 — Apply Seating Template / UC_26.3 — Save Seating Chart as Template.
    public interface ISeatingTemplateService
    {
        // Templates the caller can use: their own plus every public (admin-provided) one.
        Task<List<SeatingTemplateListDTO>> GetVisibleAsync(int callerId, bool isAdmin = false);

        Task<SeatingTemplateResponseDTO> GetByIdAsync(int id, int callerId, bool isAdmin);

        // UC_26.3 — snapshots the current, already-built layout of one of my concerts.
        Task<SeatingTemplateResponseDTO> SaveFromEventAsync(SaveSeatingTemplateDTO dto, int callerId, bool isAdmin);

        // Draw-from-scratch template, not tied to any concert.
        Task<SeatingTemplateResponseDTO> CreateAsync(CreateSeatingTemplateDTO dto, int callerId, bool isAdmin);

        Task<SeatingTemplateResponseDTO> UpdateAsync(int id, UpdateSeatingTemplateDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int id, int callerId, bool isAdmin);

        // UC_26.2 — maps every template zone onto one of the target concert's ticket
        // types, then builds the concert's seating chart from it.

        Task<SeatingChartResponseDTO> ApplyToEventAsync(
            int templateId, int eventId, ApplySeatingTemplateDTO dto, int callerId, bool isAdmin);
    }
}
