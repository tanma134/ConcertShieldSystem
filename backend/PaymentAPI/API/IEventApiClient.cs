namespace PaymentAPI.API
{
    public interface IEventApiClient
    {
        Task<int> GetOrganizerIdByEventIdAsync(int eventId);
    }
}
