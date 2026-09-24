using EventAPI.DTOs;

namespace EventAPI.Services
{
    /// <summary>
    /// Thrown when Submit/Approve is blocked by incomplete publication data.
    /// Carries the full error list so the controller can return every problem at
    /// once instead of making the customer fix them one request at a time.
    /// </summary>
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
