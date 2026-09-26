using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NotificationAPI.DTOs
{
    public class NotificationResponseDTO
    {
        [JsonPropertyName("notificationId")]
        public int NotificationId { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("message")]
        public string Message { get; set; } = null!;

        [JsonPropertyName("category")]
        public string Category { get; set; } = null!;

        [JsonPropertyName("targetUrl")]
        public string? TargetUrl { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        [JsonPropertyName("readAt")]
        public DateTime? ReadAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationListResponseDTO
    {
        [JsonPropertyName("items")]
        public List<NotificationResponseDTO> Items { get; set; } = new();

        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }
    }

    public class CreateNotificationDTO
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("message")]
        public string Message { get; set; } = null!;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "event_new";

        [JsonPropertyName("targetUrl")]
        public string? TargetUrl { get; set; }
    }

    public class BroadcastEventNotificationDTO
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = null!;

        [JsonPropertyName("posterUrl")]
        public string? PosterUrl { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
