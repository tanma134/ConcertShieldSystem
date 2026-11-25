using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CheckinAPI.API
{
    public class EventApiClient : IEventApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EventApiClient> _logger;

        public EventApiClient(HttpClient httpClient, ILogger<EventApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<EventInfoResult?> GetEventInfoAsync(int eventId)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"api/events/{eventId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("EventAPI returned status {StatusCode} for EventId {EventId}", response.StatusCode, eventId);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<EventInfoResult>(data.GetRawText(), options);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling EventAPI for EventId {EventId}", eventId);
                return null;
            }
        }

        public async Task<List<EventGateInfo>> GetEventGatesAsync(int eventId)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"api/events/{eventId}/gates");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("EventAPI returned status {StatusCode} when fetching gates for event {EventId}", response.StatusCode, eventId);
                    return new List<EventGateInfo>();
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<List<EventGateInfo>>(data.GetRawText(), options) ?? new List<EventGateInfo>();
                }

                return new List<EventGateInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching gates from EventAPI for event {EventId}", eventId);
                return new List<EventGateInfo>();
            }
        }

        public async Task<StaffAssignmentInfo?> GetStaffAssignmentAsync(int eventId, int staffUserId)
        {
            if (staffUserId <= 0) return null;
            try
            {
                using var response = await _httpClient.GetAsync($"api/events/staff/{staffUserId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("EventAPI returned status {StatusCode} when fetching assignments for staff {StaffUserId}", response.StatusCode, staffUserId);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var assignments = JsonSerializer.Deserialize<List<StaffAssignmentInfo>>(data.GetRawText(), options);
                    if (assignments != null && assignments.Count > 0)
                    {
                        var matched = eventId > 0
                            ? assignments.FirstOrDefault(a => a.EventId == eventId)
                            : assignments.FirstOrDefault();

                        return matched ?? (eventId <= 0 ? assignments[0] : null);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not fetch staff assignment from EventAPI for user {StaffUserId}", staffUserId);
                return null;
            }
        }
    }
}
