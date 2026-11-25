namespace EventAPI.DTOs;

// Du lieu dau vao khi Organizer gui yeu cau thay doi hoac hoan lich su kien
public record ChangeInput(string Type, string Reason, DateTimeOffset? NewStartsAt, DateTimeOffset? NewEndsAt);

// Du lieu dau vao khi Admin chu dong hoan hoac doi ngay truc tiep trong tinh huong khan cap
public record AdminDirectChangeInput(string Type, string Reason, DateTimeOffset? NewStartsAt, DateTimeOffset? NewEndsAt);

// Du lieu dau vao khi Admin danh gia ho so phap ly
public record ComplianceReviewInput(int Version, string Decision, string Notes);

// Du lieu dau vao khi Admin phe duyet hoac tu choi yeu cau thay doi lich
public record ChangeReviewInput(string Decision, string Notes);

// Thong tin kiem tra dieu kien ban ve cua su kien phuc vu TicketAPI
public record SaleEligibility(bool CanSell, string Reason, string Status, DateTime StartsAt, DateTime EndsAt, int ScheduleVersion, bool RequiresVirtualQueue);

// Thong tin thong bao he thong gui toi nguoi dung qua outbox
public record NotificationMessage(int UserId, string Title, string Message, string Category, string TargetUrl);

// Thong tin yeu cau ap dung thay doi lich va freeze ve dong bo sang TicketAPI
public record ApplyChangeMessage(long ChangeId, int EventId, string Type, DateTime? StartsAt, DateTime? EndsAt, int ScheduleVersion, string Reason, string Title, string Slug);
