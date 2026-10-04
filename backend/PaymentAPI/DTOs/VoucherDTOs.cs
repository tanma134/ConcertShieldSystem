using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PaymentAPI.DTOs
{
    public class CreateVoucherDTO
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = null!;

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = null!; // "SYSTEM", "ORGANIZER", "EVENT"

        [JsonPropertyName("organizer_id")]
        public int? OrganizerId { get; set; }

        [JsonPropertyName("event_id")]
        public int? EventId { get; set; }

        [JsonPropertyName("discount_type")]
        public string DiscountType { get; set; } = null!; // "PERCENT", "FIXED"

        [JsonPropertyName("discount_amount")]
        public long? DiscountAmount { get; set; }

        [JsonPropertyName("discount_percent")]
        public decimal? DiscountPercent { get; set; }

        [JsonPropertyName("max_discount_amount")]
        public long? MaxDiscountAmount { get; set; }

        [JsonPropertyName("min_order_amount")]
        public long? MinOrderAmount { get; set; } = 0;

        [JsonPropertyName("total_quantity")]
        public int TotalQuantity { get; set; }

        [JsonPropertyName("max_usage_per_user")]
        public int MaxUsagePerUser { get; set; } = 1;

        [JsonPropertyName("starts_at")]
        public DateTime StartsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime EndsAt { get; set; }
    }

    public class VoucherResponseDTO
    {
        [JsonPropertyName("voucher_id")]
        public int VoucherId { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = null!;

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = null!;

        [JsonPropertyName("organizer_id")]
        public int? OrganizerId { get; set; }

        [JsonPropertyName("event_id")]
        public int? EventId { get; set; }

        [JsonPropertyName("discount_type")]
        public string DiscountType { get; set; } = null!;

        [JsonPropertyName("discount_amount")]
        public long? DiscountAmount { get; set; }

        [JsonPropertyName("discount_percent")]
        public decimal? DiscountPercent { get; set; }

        [JsonPropertyName("max_discount_amount")]
        public long? MaxDiscountAmount { get; set; }

        [JsonPropertyName("min_order_amount")]
        public long MinOrderAmount { get; set; }

        [JsonPropertyName("total_quantity")]
        public int TotalQuantity { get; set; }

        [JsonPropertyName("used_quantity")]
        public int UsedQuantity { get; set; }

        [JsonPropertyName("max_usage_per_user")]
        public int MaxUsagePerUser { get; set; }

        [JsonPropertyName("starts_at")]
        public DateTime StartsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime EndsAt { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Active"; // "Active", "Inactive", "Expired"

        [JsonPropertyName("created_by")]
        public int CreatedBy { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class VoucherUsageDTO
    {
        [JsonPropertyName("voucher_usage_id")]
        public int VoucherUsageId { get; set; }

        [JsonPropertyName("voucher_id")]
        public int VoucherId { get; set; }

        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("discount_amount")]
        public long DiscountAmount { get; set; }

        [JsonPropertyName("used_at")]
        public DateTime UsedAt { get; set; }
    }

    public class UpdateVoucherStatusDTO
    {
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }

    public class UpdateVoucherDTO
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("organizer_id")]
        public int? OrganizerId { get; set; }

        [JsonPropertyName("event_id")]
        public int? EventId { get; set; }

        [JsonPropertyName("discount_type")]
        public string? DiscountType { get; set; }

        [JsonPropertyName("discount_amount")]
        public long? DiscountAmount { get; set; }

        [JsonPropertyName("discount_percent")]
        public decimal? DiscountPercent { get; set; }

        [JsonPropertyName("max_discount_amount")]
        public long? MaxDiscountAmount { get; set; }

        [JsonPropertyName("min_order_amount")]
        public long? MinOrderAmount { get; set; }

        [JsonPropertyName("total_quantity")]
        public int? TotalQuantity { get; set; }

        [JsonPropertyName("max_usage_per_user")]
        public int? MaxUsagePerUser { get; set; }

        [JsonPropertyName("starts_at")]
        public DateTime? StartsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime? EndsAt { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
    }

    public class ApiResponseDTO<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponseDTO<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponseDTO<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponseDTO<T> FailResponse(string message, List<string>? errors = null)
        {
            return new ApiResponseDTO<T> { Success = false, Message = message, Errors = errors };
        }
    }

    public class PagedResultDTO<T>
    {
        [JsonPropertyName("items")]
        public List<T> Items { get; set; } = new List<T>();

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("page_size")]
        public int PageSize { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

        [JsonPropertyName("has_previous_page")]
        public bool HasPreviousPage => Page > 1;

        [JsonPropertyName("has_next_page")]
        public bool HasNextPage => Page < TotalPages;
    }
}
