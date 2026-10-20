using System.Text.Json.Serialization;
using PaymentAPI.DTOs;

namespace PaymentAPI.API
{
    public class EventApiClient : IEventApiClient
    {
        private readonly HttpClient _httpClient;

        public EventApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<int> GetOrganizerIdByEventIdAsync(int eventId)
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponseDTO<EventOrganizerDTO>>($"api/Events/{eventId}");

            var organizerId = response?.Data?.OrganizerId;

            if (organizerId is null or <= 0)
                throw new KeyNotFoundException(
                    $"Không tìm thấy organizer của event {eventId}.");

            return organizerId.Value;
        }

        private sealed class EventOrganizerDTO
        {
            [JsonPropertyName("organizerId")]
            public int? OrganizerId { get; set; }
        }
    }
}
