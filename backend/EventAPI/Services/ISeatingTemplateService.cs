using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ISeatingTemplateService
    {
        Task<List<SeatingTemplateListDTO>> GetVisibleAsync(int callerId, bool isAdmin = false);

        Task<SeatingTemplateResponseDTO> GetByIdAsync(int id, int callerId, bool isAdmin);

        Task<SeatingTemplateResponseDTO> SaveFromEventAsync(SaveSeatingTemplateDTO dto, int callerId, bool isAdmin);

        Task<SeatingTemplateResponseDTO> CreateAsync(CreateSeatingTemplateDTO dto, int callerId, bool isAdmin);

        Task<SeatingTemplateResponseDTO> UpdateAsync(int id, UpdateSeatingTemplateDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int id, int callerId, bool isAdmin);

        Task<SeatingChartResponseDTO> ApplyToEventAsync(
            int templateId, int eventId, ApplySeatingTemplateDTO dto, int callerId, bool isAdmin);
    }
}
