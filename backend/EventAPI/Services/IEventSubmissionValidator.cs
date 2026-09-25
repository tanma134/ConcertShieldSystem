using EventAPI.DTOs;

namespace EventAPI.Services
{

    // Draft concerts are deliberately allowed to be incomplete. This validator is
    // the single gate that runs right before Draft/Rejected -> Pending, so an Admin
    // never sees a half-filled submission.

    public interface IEventSubmissionValidator
    {
        Task<SubmitValidationResultDTO> ValidateAsync(int eventId);
    }
}
