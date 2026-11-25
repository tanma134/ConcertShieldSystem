using EventAPI.DTOs;

namespace EventAPI.Services
{
    // Thrown when Submit/Approve is blocked; carries all validation errors at once.
    public class SubmissionValidationException : Exception
    {
        public SubmitValidationResultDTO Result { get; }

        public SubmissionValidationException(SubmitValidationResultDTO result)
            : base("The concert is missing required publication data.")
        {
            Result = result;
        }
    }
}
