using BookingAPI.DTOS;
using System.Text.Json;
using static TicketAPI.DTOs.ConfirmEventSaleDto;

namespace TicketAPI.API
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

        public async Task<EventApiResponseDto?> GetEventByIdAsync(int eventId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/events/{eventId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Cannot get event info for EventId {EventId}. Status: {StatusCode}", eventId, response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var jsonDoc = JsonDocument.Parse(json);
                if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement dataElement))
                {
                    return JsonSerializer.Deserialize<EventApiResponseDto>(dataElement.GetRawText(), options);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event with ID {EventId}", eventId);
                return null;
            }
        }

        // Reads an event through the caller's own token (api/events/mine/{id}), so EventAPI
        // itself decides whether the caller owns it. Returns null when it cannot be read.
        public async Task<EventApiResponseDto?> GetOwnedEventAsync(int eventId, string? bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"api/events/mine/{eventId}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var jsonDoc = JsonDocument.Parse(json);
                return jsonDoc.RootElement.TryGetProperty("data", out var data)
                    ? JsonSerializer.Deserialize<EventApiResponseDto>(data.GetRawText(), options)
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ownership of event {EventId}", eventId);
                return null;
            }
        }

        public async Task<bool> ConfirmPaidOrderSaleAsync(int eventId, ConfirmEventSaleRequestDto request)
        {
            try
            {
                using var response = await _httpClient.PutAsJsonAsync(
                    $"api/events/{eventId}/orders/{request.OrderId}/confirm-sale",
                    request);

                if (response.IsSuccessStatusCode)
                    return true;

                var body = await response.Content.ReadAsStringAsync();

                _logger.LogError(
                    "EventAPI không cập nhật được số vé đã bán cho Order {OrderId}. Status: {StatusCode}, Body: {Body}",
                    request.OrderId,
                    response.StatusCode,
                    body);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi gọi EventAPI cập nhật số vé đã bán cho Order {OrderId}",
                    request.OrderId);

                return false;
            }
        }

        //public async Task<TicketTypeApiResponseDto?> GetTicketTypeByIdAsync(int eventId, int ticketTypeId)
        //{
        //    try
        //    {
        //        // 1. Gọi API lấy sự kiện
        //        var response = await GetEventByIdAsync(eventId);
        //        if (response == null) return null;

        //        // 2. Dùng dynamic để lấy phần Data (bất kể response là DTO hay JsonElement)
        //        dynamic eventData = response;

        //        // Nếu response có property "Data" (là Wrapper), lấy Data. Nếu không, lấy chính response.
        //        if (response.GetType().GetProperty("Data") != null)
        //        {
        //            eventData = response.Data;
        //        }
        //        else if (response is System.Text.Json.JsonElement jsonElement)
        //        {
        //            // Trường hợp API trả về raw JSON
        //            if (jsonElement.TryGetProperty("data", out var dataProp))
        //                eventData = dataProp;
        //        }

        //        if (eventData == null || eventData.TicketTypes == null)
        //        {
        //            _logger.LogWarning("Không tìm thấy TicketTypes trong response của EventId {EventId}", eventId);
        //            return null;
        //        }

        //        // 3. Duyệt qua danh sách TicketTypes (dynamic duyệt rất mượt)
        //        foreach (var t in eventData.TicketTypes)
        //        {
        //            // Ép kiểu động để so sánh ID
        //            if ((int)t.TicketTypeId == ticketTypeId)
        //            {
        //                // 4. Map sang DTO chuẩn để trả về
        //                return new TicketTypeApiResponseDto
        //                {
        //                    TicketTypeId = t.TicketTypeId,
        //                    TypeName = t.TypeName,
        //                    Price = t.Price,
        //                    Quantity = t.Quantity,
        //                    Status = t.Status
        //                };
        //            }
        //        }

        //        _logger.LogWarning("Không tìm thấy TicketTypeId {TicketTypeId} trong EventId {EventId}", ticketTypeId, eventId);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi trong GetTicketTypeByIdAsync");
        //        return null;
        //    }
        //}
        public async Task<bool> ReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity)
            => (await TryReleaseReturnedTicketAsync(eventId, ticketTypeId, quantity)).Success;

        public async Task<InventoryReleaseOutcome> TryReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity)
        {
            try
            {
                using var response = await _httpClient.PutAsync(
                    $"api/events/{eventId}/ticket-types/{ticketTypeId}/release-returned?quantity={quantity}", null);
                if (response.IsSuccessStatusCode)
                    return new InventoryReleaseOutcome(true, false, null);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("EventAPI has no ticket type {TicketTypeId} for event {EventId}; nothing to release.", ticketTypeId, eventId);
                    return new InventoryReleaseOutcome(false, true, $"Ticket type {ticketTypeId} of event {eventId} was not found in EventAPI.");
                }

                _logger.LogError("EventAPI failed to release returned ticket. Event {EventId}, type {TicketTypeId}, status {Status}", eventId, ticketTypeId, response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return new InventoryReleaseOutcome(false, false,
                        "EventAPI rejected the internal API key (TicketAPI InternalApiKey must equal EventAPI EventApi:InternalApiKey).");

                return new InventoryReleaseOutcome(false, false, $"EventAPI returned HTTP {(int)response.StatusCode}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventAPI release-returned call failed for event {EventId}, type {TicketTypeId}", eventId, ticketTypeId);
                return new InventoryReleaseOutcome(false, false,
                    $"EventAPI is not reachable at {_httpClient.BaseAddress} ({ex.GetType().Name}: {ex.Message}).");
            }
        }

        // Reads the organizer's refund policies for an event and keeps the active ones.
        // On any failure it returns an empty list, so the return flow falls back to the
        // default tier instead of blocking every customer.
        public async Task<IReadOnlyList<TicketAPI.Domain.RefundTier>> GetRefundTiersAsync(int eventId)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"api/refundpolicies/event/{eventId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cannot read refund policies for event {EventId}. Status: {Status}", eventId, response.StatusCode);
                    return Array.Empty<TicketAPI.Domain.RefundTier>();
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return Array.Empty<TicketAPI.Domain.RefundTier>();

                var tiers = new List<TicketAPI.Domain.RefundTier>();
                foreach (var item in data.EnumerateArray())
                {
                    var active = !item.TryGetProperty("isActive", out var a) || a.ValueKind != JsonValueKind.False;
                    if (!active) continue;
                    if (!item.TryGetProperty("deadlineBeforeEventHours", out var h) || !h.TryGetInt32(out var hours)) continue;
                    if (!item.TryGetProperty("refundPercent", out var p) || !p.TryGetDecimal(out var percent)) continue;
                    tiers.Add(new TicketAPI.Domain.RefundTier(hours, percent));
                }

                return tiers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading refund policies for event {EventId}", eventId);
                return Array.Empty<TicketAPI.Domain.RefundTier>();
            }
        }

        public async Task<IReadOnlyList<int>> GetReturnReviewEventIdsAsync(int staffUserId)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"api/internal/staff/{staffUserId}/return-review-events");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cannot read return-review events for staff {StaffId}. Status: {Status}", staffUserId, response.StatusCode);
                    return Array.Empty<int>();
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return Array.Empty<int>();

                var ids = new List<int>();
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetInt32(out var id)) ids.Add(id);
                return ids;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading return-review events for staff {StaffId}", staffUserId);
                return Array.Empty<int>();
            }
        }
    }
}
