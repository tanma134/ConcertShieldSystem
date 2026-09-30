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
    }
}
