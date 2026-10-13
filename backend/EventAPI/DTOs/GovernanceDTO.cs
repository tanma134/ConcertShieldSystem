namespace EventAPI.DTOs;
public record ChangeInput(string Type, string Reason, DateTimeOffset? NewStartsAt, DateTimeOffset? NewEndsAt);
public record ComplianceReviewInput(int Version, string Decision, string Notes);
public record ChangeReviewInput(string Decision, string Notes);
public record SaleEligibility(bool CanSell, string Reason, string Status, DateTime StartsAt, DateTime EndsAt, int ScheduleVersion);
public record NotificationMessage(int UserId, string Title, string Message, string Category, string TargetUrl);
public record ApplyChangeMessage(long ChangeId, int EventId, string Type, DateTime? StartsAt, DateTime? EndsAt, int ScheduleVersion, string Reason, string Title, string Slug);
