using EventAPI.DTOs;

namespace EventAPI.Services
{
    // Single validation gate run before Draft/Rejected -> Pending so incomplete drafts cannot reach Admin review.
    public interface IEventSubmissionValidator
    {
        Task<SubmitValidationResultDTO> ValidateAsync(int eventId);
    }
}
