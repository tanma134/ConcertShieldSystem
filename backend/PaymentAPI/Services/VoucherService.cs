using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly IVoucherRepository _voucherRepository;
        private readonly ITicketApiClient _ticketApiClient;
        private readonly IEventApiClient _eventApiClient;

        private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;

        public VoucherService(IVoucherRepository voucherRepository, IEventApiClient eventApiClient, ITicketApiClient ticketApiClient, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        {
            _voucherRepository = voucherRepository;
            _eventApiClient = eventApiClient;
            _ticketApiClient = ticketApiClient;
            _configuration = configuration;
        }

        public async Task<VoucherResponseDTO> CreateVoucherAsync(CreateVoucherDTO dto, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new ArgumentException("Voucher code cannot be empty.");
            }

            var trimmedCode = dto.Code.Trim();
            if (trimmedCode.Contains(' '))
            {
                throw new ArgumentException("Voucher code cannot contain whitespace.");
            }

            var normalizedCode = trimmedCode.ToUpperInvariant();

            if (await _voucherRepository.CodeExistsAsync(normalizedCode))
            {
                throw new InvalidOperationException($"Voucher code '{normalizedCode}' already exists in the system.");
            }

            var scope = (dto.Scope ?? "").Trim().ToUpperInvariant();
            if (scope != "SYSTEM" && scope != "ORGANIZER" && scope != "EVENT")
            {
                throw new ArgumentException("Invalid voucher scope. Allowed values: SYSTEM, ORGANIZER, or EVENT.");
            }

            int? organizerId = dto.OrganizerId;
            int? eventId = dto.EventId;

            if (scope == "SYSTEM")
            {
                if (!isAdmin)
                {
                    throw new UnauthorizedAccessException("Only System Administrators can create SYSTEM vouchers.");
                }
                organizerId = null;
                eventId = null;
            }
            else if (scope == "ORGANIZER")
            {
                if (!organizerId.HasValue || organizerId.Value <= 0)
                {
                    organizerId = currentUserId;
                }
                eventId = null;
            }
            else if (scope == "EVENT")
            {
                if (!eventId.HasValue || eventId.Value <= 0)
                {
                    throw new ArgumentException("Event ID is required when creating an EVENT scope voucher.");
                }
                if (!organizerId.HasValue || organizerId.Value <= 0)
                {
                    organizerId = currentUserId;
                }
            }

            var discountType = (dto.DiscountType ?? "").Trim().ToUpperInvariant();
            if (discountType != "PERCENT" && discountType != "FIXED")
            {
                throw new ArgumentException("Invalid discount type. Allowed values: PERCENT or FIXED.");
            }

            long? discountAmount = null;
            decimal? discountPercent = null;
            long? maxDiscountAmount = null;

            if (discountType == "PERCENT")
            {
                if (!dto.DiscountPercent.HasValue || dto.DiscountPercent.Value <= 0 || dto.DiscountPercent.Value > 100)
                {
                    throw new ArgumentException("Discount percentage must be greater than 0 and not exceed 100%.");
                }
                discountPercent = dto.DiscountPercent.Value;
                maxDiscountAmount = dto.MaxDiscountAmount.HasValue && dto.MaxDiscountAmount.Value > 0 
                    ? dto.MaxDiscountAmount.Value 
                    : null;
            }
            else
            {
                if (!dto.DiscountAmount.HasValue || dto.DiscountAmount.Value <= 0)
                {
                    throw new ArgumentException("Fixed discount amount must be greater than 0.");
                }
                discountAmount = dto.DiscountAmount.Value;
            }

            var startsAt = dto.StartsAt.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dto.StartsAt, DateTimeKind.Utc) 
                : dto.StartsAt.ToUniversalTime();

            var endsAt = dto.EndsAt.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dto.EndsAt, DateTimeKind.Utc) 
                : dto.EndsAt.ToUniversalTime();

            if (startsAt >= endsAt)
            {
                throw new ArgumentException("Start date (starts_at) must be earlier than end date (ends_at).");
            }

            if (dto.TotalQuantity <= 0)
            {
                throw new ArgumentException("Total voucher quantity must be greater than 0.");
            }

            var maxUsagePerUser = dto.MaxUsagePerUser < 1 ? 1 : dto.MaxUsagePerUser;
            var minOrderAmount = dto.MinOrderAmount.HasValue && dto.MinOrderAmount.Value > 0 
                ? dto.MinOrderAmount.Value 
                : 0L;

            var voucher = new Voucher
            {
                Code = normalizedCode,
                Scope = scope,
                OrganizerId = organizerId,
                EventId = eventId,
                DiscountType = discountType,
                DiscountAmount = discountAmount,
                DiscountPercent = discountPercent,
                MaxDiscountAmount = maxDiscountAmount,
                MinOrderAmount = minOrderAmount,
                TotalQuantity = dto.TotalQuantity,
                UsedQuantity = 0,
                MaxUsagePerUser = maxUsagePerUser,
                StartsAt = startsAt,
                EndsAt = endsAt,
                IsActive = true,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            var created = await _voucherRepository.CreateAsync(voucher);

            var createdCode = created.Code;
            var createdEventId = created.EventId;
            var creatorId = currentUserId;

            _ = Task.Run(async () =>
            {
                var targetUrl = createdEventId.HasValue ? $"/events/{createdEventId}" : "/";

                // Persist directly to notification_db before HTTP ping
                try
                {
                    var notifConnStr = _configuration?.GetConnectionString("NotificationConnection")
                        ?? _configuration?.GetConnectionString("NotificationDb")
                        ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_NOTIFICATION_POSTGRES")
                        ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES");
                    if (string.IsNullOrWhiteSpace(notifConnStr)) return;
                    using var conn = new Npgsql.NpgsqlConnection(notifConnStr);
                    await conn.OpenAsync();

                    // Notification 1: Broadcast to all customers (UserId = 0)
                    using (var cmd = new Npgsql.NpgsqlCommand(
                        "INSERT INTO notifications (user_id, title, message, content, type, category, target_url, is_read, created_at, is_deleted) VALUES (@uid, @title, @msg, @content, @type, @cat, @url, false, NOW(), false);", conn))
                    {
                        var title = $"New Voucher: {createdCode}";
                        var msg = $"Special discount voucher '{createdCode}' is now available! Apply it at checkout to get discount.";
                        cmd.Parameters.AddWithValue("uid", 0);
                        cmd.Parameters.AddWithValue("title", title);
                        cmd.Parameters.AddWithValue("msg", msg);
                        cmd.Parameters.AddWithValue("content", msg);
                        cmd.Parameters.AddWithValue("type", "voucher_new");
                        cmd.Parameters.AddWithValue("cat", "voucher_new");
                        cmd.Parameters.AddWithValue("url", targetUrl);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Notification 2: Confirmation for Organizer / Creator
                    if (creatorId > 0)
                    {
                        using var cmd2 = new Npgsql.NpgsqlCommand(
                            "INSERT INTO notifications (user_id, title, message, content, type, category, target_url, is_read, created_at, is_deleted) VALUES (@uid, @title, @msg, @content, @type, @cat, @url, false, NOW(), false);", conn);
                        var title = $"Voucher Created: {createdCode}";
                        var msg = $"Your promotional voucher '{createdCode}' was created successfully and is now active.";
                        cmd2.Parameters.AddWithValue("uid", creatorId);
                        cmd2.Parameters.AddWithValue("title", title);
                        cmd2.Parameters.AddWithValue("msg", msg);
                        cmd2.Parameters.AddWithValue("content", msg);
                        cmd2.Parameters.AddWithValue("type", "voucher_new");
                        cmd2.Parameters.AddWithValue("cat", "voucher_new");
                        cmd2.Parameters.AddWithValue("url", "/organizer/vouchers");
                        await cmd2.ExecuteNonQueryAsync();
                    }
                }
                catch
                {
                }

                try
                {
                    var handler = new System.Net.Http.HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                    };
                    using var httpClient = new System.Net.Http.HttpClient(handler);
                    httpClient.Timeout = TimeSpan.FromSeconds(3);

                    var payload = new
                    {
                        userId = 0,
                        title = $"New Voucher: {createdCode}",
                        message = $"Special discount voucher '{createdCode}' is now available! Apply it at checkout to get discount.",
                        category = "voucher_new",
                        targetUrl = targetUrl
                    };
                    var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                    var notificationApiUrl = _configuration?["NotificationApi:BaseUrl"] ?? "https://localhost:7197";
                    var endpoints = new[] { $"{notificationApiUrl.TrimEnd('/')}/api/notifications" };

                    foreach (var ep in endpoints)
                    {
                        try
                        {
                            var res = await httpClient.PostAsync(ep, content);
                            if (res.IsSuccessStatusCode) break;
                        }
                        catch { }
                    }
                }
                catch
                {
                    // Ignore background ping errors
                }
            });

            return MapToResponseDTO(created);
        }

        public async Task<PagedResultDTO<VoucherResponseDTO>> GetVouchersAsync(
            int page,
            int limit,
            string? scope,
            bool? isActive,
            string? searchCode,
            int? organizerId,
            int currentUserId,
            bool isAdmin,
            bool isOrganizer)
        {
            if (page <= 0)
            {
                throw new ArgumentException("Page must be greater than 0.", nameof(page));
            }
            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than 0.", nameof(limit));
            }

            bool isOrganizerOnly = false;
            int? queryOrgId = organizerId;

            if (!isAdmin && isOrganizer)
            {
                isOrganizerOnly = true;
                queryOrgId = currentUserId;
            }
            else if (organizerId.HasValue)
            {
                isOrganizerOnly = true;
            }
            else if (!isAdmin && !isOrganizer)
            {
                throw new UnauthorizedAccessException("You do not have permission to view the voucher catalog.");
            }

            var paged = await _voucherRepository.GetPagedAsync(
                page,
                limit,
                scope,
                isActive,
                searchCode,
                queryOrgId,
                isOrganizerOnly);

            return new PagedResultDTO<VoucherResponseDTO>
            {
                Items = paged.Items.Select(MapToResponseDTO).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize
            };
        }

        public async Task<VoucherResponseDTO> GetVoucherByIdAsync(int id, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(id);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Voucher with ID {id} not found.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to access this voucher.");
                }
            }

            return MapToResponseDTO(voucher);
        }

        public async Task<List<VoucherUsageDTO>> GetVoucherUsagesAsync(int voucherId, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(voucherId);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Voucher with ID {voucherId} not found.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this voucher's usage history.");
                }
            }

            var usages = await _voucherRepository.GetUsagesByVoucherIdAsync(voucherId);
            return usages.Select(u => new VoucherUsageDTO
            {
                VoucherUsageId = u.VoucherUsageId,
                VoucherId = u.VoucherId,
                OrderId = u.OrderId,
                UserId = u.UserId,
                DiscountAmount = u.DiscountAmount,
                UsedAt = u.UsedAt
            }).ToList();
        }

        public async Task<VoucherResponseDTO> UpdateVoucherStatusAsync(int id, bool isActive, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(id);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Voucher with ID {id} not found.");
            }

            if (!isAdmin && !isOrganizer)
            {
                throw new UnauthorizedAccessException("You do not have permission to update voucher status.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.Scope == "SYSTEM")
                {
                    throw new UnauthorizedAccessException("Organizers cannot update system-level vouchers.");
                }
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to update this voucher status.");
                }
            }

            voucher.IsActive = isActive;
            var updated = await _voucherRepository.UpdateAsync(voucher);
            return MapToResponseDTO(updated);
        }

        public async Task<VoucherResponseDTO> UpdateVoucherAsync(int id, UpdateVoucherDTO dto, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(id);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Voucher with ID {id} not found.");
            }

            if (!isAdmin && !isOrganizer)
            {
                throw new UnauthorizedAccessException("You do not have permission to update vouchers.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.Scope == "SYSTEM")
                {
                    throw new UnauthorizedAccessException("Organizers cannot update system-level vouchers.");
                }
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to update this voucher.");
                }
                if (!string.IsNullOrWhiteSpace(dto.Scope) && dto.Scope.Trim().ToUpperInvariant() == "SYSTEM")
                {
                    throw new UnauthorizedAccessException("Organizers cannot elevate voucher scope to SYSTEM.");
                }
            }

            var now = DateTime.UtcNow;

            // 1. Immutable fields when voucher has been used (UsedQuantity > 0)
            if (!string.IsNullOrWhiteSpace(dto.Code))
            {
                var trimmedCode = dto.Code.Trim().ToUpperInvariant();
                if (trimmedCode != voucher.Code)
                {
                    if (voucher.UsedQuantity > 0)
                    {
                        throw new InvalidOperationException("Voucher code cannot be modified once the voucher has been used.");
                    }
                    if (await _voucherRepository.CodeExistsAsync(trimmedCode, id))
                    {
                        throw new InvalidOperationException($"Voucher code '{trimmedCode}' already exists.");
                    }
                    voucher.Code = trimmedCode;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Scope))
            {
                var scope = dto.Scope.Trim().ToUpperInvariant();
                if (scope != voucher.Scope)
                {
                    if (voucher.UsedQuantity > 0)
                    {
                        throw new InvalidOperationException("Voucher scope cannot be modified once the voucher has been used.");
                    }
                    if (scope == "SYSTEM" || scope == "ORGANIZER" || scope == "EVENT")
                    {
                        voucher.Scope = scope;
                    }
                }
            }

            if (dto.OrganizerId.HasValue) voucher.OrganizerId = dto.OrganizerId.Value;
            if (dto.EventId.HasValue) voucher.EventId = dto.EventId.Value;

            if (!string.IsNullOrWhiteSpace(dto.DiscountType))
            {
                var type = dto.DiscountType.Trim().ToUpperInvariant();
                if (type != voucher.DiscountType)
                {
                    if (voucher.UsedQuantity > 0)
                    {
                        throw new InvalidOperationException("Discount type cannot be modified once the voucher has been used.");
                    }
                    voucher.DiscountType = type;
                }
            }

            // 2. Total quantity constraint: cannot reduce usage limit below current used count
            if (dto.TotalQuantity.HasValue)
            {
                if (dto.TotalQuantity.Value < voucher.UsedQuantity)
                {
                    throw new ArgumentException($"Cannot reduce usage limit below current used count ({voucher.UsedQuantity}).");
                }
                voucher.TotalQuantity = dto.TotalQuantity.Value;
            }

            // 3. Timeframe constraints
            var newStartsAt = dto.StartsAt.HasValue
                ? (dto.StartsAt.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dto.StartsAt.Value, DateTimeKind.Utc)
                    : dto.StartsAt.Value.ToUniversalTime())
                : voucher.StartsAt;

            var newEndsAt = dto.EndsAt.HasValue
                ? (dto.EndsAt.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dto.EndsAt.Value, DateTimeKind.Utc)
                    : dto.EndsAt.Value.ToUniversalTime())
                : voucher.EndsAt;

            if (newEndsAt <= newStartsAt)
            {
                throw new ArgumentException("End date (ends_at) must be later than start date (starts_at).");
            }

            // starts_at constraint: If voucher is active/running (starts_at < now) and used_quantity > 0, cannot postpone starts_at to future
            if (dto.StartsAt.HasValue && voucher.StartsAt < now && voucher.UsedQuantity > 0)
            {
                if (newStartsAt > now)
                {
                    throw new InvalidOperationException("Cannot postpone start date to the future for an ongoing voucher that has already been used.");
                }
            }

            // ends_at shortening constraint: New ends_at must be > now. (To deactivate immediately, set is_active to false)
            if (dto.EndsAt.HasValue && newEndsAt <= now)
            {
                throw new InvalidOperationException("End date must be in the future (after current update time). To deactivate voucher immediately, set status to Inactive.");
            }

            voucher.StartsAt = newStartsAt;
            voucher.EndsAt = newEndsAt;

            if (dto.DiscountAmount.HasValue && dto.DiscountAmount.Value != voucher.DiscountAmount)
            {
                if (voucher.UsedQuantity > 0)
                {
                    throw new InvalidOperationException("Discount amount cannot be modified once the voucher has been used.");
                }
                voucher.DiscountAmount = dto.DiscountAmount.Value;
            }

            if (dto.DiscountPercent.HasValue && dto.DiscountPercent.Value != voucher.DiscountPercent)
            {
                if (voucher.UsedQuantity > 0)
                {
                    throw new InvalidOperationException("Discount percentage cannot be modified once the voucher has been used.");
                }
                voucher.DiscountPercent = dto.DiscountPercent.Value;
            }

            if (dto.MaxDiscountAmount.HasValue && dto.MaxDiscountAmount.Value != voucher.MaxDiscountAmount)
            {
                if (voucher.UsedQuantity > 0)
                {
                    throw new InvalidOperationException("Max discount amount cannot be modified once the voucher has been used.");
                }
                voucher.MaxDiscountAmount = dto.MaxDiscountAmount.Value;
            }

            if (dto.MinOrderAmount.HasValue) voucher.MinOrderAmount = dto.MinOrderAmount.Value;
            if (dto.MaxUsagePerUser.HasValue && dto.MaxUsagePerUser.Value > 0) voucher.MaxUsagePerUser = dto.MaxUsagePerUser.Value;
            if (dto.IsActive.HasValue) voucher.IsActive = dto.IsActive.Value;

            var updated = await _voucherRepository.UpdateAsync(voucher);
            return MapToResponseDTO(updated);
        }

        public async Task<bool> SoftDeleteVoucherAsync(int id, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(id);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Voucher with ID {id} not found.");
            }

            if (!isAdmin && !isOrganizer)
            {
                throw new UnauthorizedAccessException("You do not have permission to delete vouchers.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.Scope == "SYSTEM")
                {
                    throw new UnauthorizedAccessException("Organizers cannot delete system-level vouchers.");
                }
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to delete this voucher.");
                }
            }

            // Block deletion if the voucher has already been used
            if (voucher.UsedQuantity > 0)
            {
                throw new InvalidOperationException($"Cannot delete a voucher that has already been used ({voucher.UsedQuantity} time(s)). Deactivate it instead.");
            }

            // Soft-delete: mark inactive and deleted to avoid foreign key constraints
            voucher.IsActive = false;
            voucher.IsDeleted = true;
            voucher.DeletedAt = DateTime.UtcNow;
            voucher.DeletedBy = currentUserId;

            await _voucherRepository.UpdateAsync(voucher);
            return true;
        }

        private static VoucherResponseDTO MapToResponseDTO(Voucher v)
        {
            string status;
            var now = DateTime.UtcNow;

            if (v.EndsAt < now)
            {
                status = "Expired";
            }
            else if (!v.IsActive)
            {
                status = "Inactive";
            }
            else
            {
                status = "Active";
            }

            return new VoucherResponseDTO
            {
                VoucherId = v.VoucherId,
                Code = v.Code,
                Scope = v.Scope,
                OrganizerId = v.OrganizerId,
                EventId = v.EventId,
                DiscountType = v.DiscountType,
                DiscountAmount = v.DiscountAmount,
                DiscountPercent = v.DiscountPercent,
                MaxDiscountAmount = v.MaxDiscountAmount,
                MinOrderAmount = v.MinOrderAmount ?? 0,
                TotalQuantity = v.TotalQuantity,
                UsedQuantity = v.UsedQuantity,
                MaxUsagePerUser = v.MaxUsagePerUser,
                StartsAt = v.StartsAt,
                EndsAt = v.EndsAt,
                IsActive = v.IsActive,
                Status = status,
                CreatedBy = v.CreatedBy,
                CreatedAt = v.CreatedAt
            };
        }

        public async Task<ApplyVoucherResponseDTO> ApplyVoucherAsync(ApplyVoucherRequestDTO request, int userId)
        {
            ApplyVoucherResponseDTO Invalid(string message) => new()
            {
                IsValid = false,
                DiscountAmount = 0,
                FinalAmount = Math.Max(0, request.OrderAmount),
                Message = message
            };

            if (string.IsNullOrWhiteSpace(request.Code))
                return Invalid("Please enter a voucher code.");

            if (request.EventId <= 0)
                return Invalid("Invalid event.");

            if (request.OrderAmount <= 0)
                return Invalid("Invalid order amount.");

            var voucher = await _voucherRepository.GetByCodeAsync(request.Code);

            if (voucher == null)
                return Invalid("Voucher not found.");

            var now = DateTime.UtcNow;

            if (!voucher.IsActive)
                return Invalid("This voucher is currently inactive.");

            if (now < voucher.StartsAt || now > voucher.EndsAt)
                return Invalid("This voucher is not yet valid or has expired.");

            if (voucher.UsedQuantity >= voucher.TotalQuantity)
                return Invalid("This voucher has reached its usage limit.");

            if (request.OrderAmount < (voucher.MinOrderAmount ?? 0))
                return Invalid("The order does not meet the minimum amount required for this voucher.");

            if (voucher.Scope == "EVENT" && voucher.EventId != request.EventId)
                return Invalid("This voucher does not apply to this event.");

            var eventOrganizerId = await _eventApiClient.GetOrganizerIdByEventIdAsync(request.EventId);

            var isApplicable = voucher.Scope switch
            {
                "SYSTEM" => true,
                "ORGANIZER" => voucher.OrganizerId == eventOrganizerId,
                "EVENT" => voucher.EventId == request.EventId,
                _ => false
            };

            if (!isApplicable)
                return Invalid("This voucher does not apply to this event.");

            if (voucher.Scope != "SYSTEM" &&
                voucher.Scope != "EVENT" &&
                voucher.Scope != "ORGANIZER")
            {
                return Invalid("Invalid voucher scope.");
            }

            var userUsageCount = await _voucherRepository.CountUserUsagesAsync(voucher.VoucherId, userId);

            if (userUsageCount >= voucher.MaxUsagePerUser)
                return Invalid("You have reached the usage limit for this voucher.");

            decimal calculatedDiscount;

            if (voucher.DiscountType == "PERCENT")
            {
                if (!voucher.DiscountPercent.HasValue)
                    return Invalid("The voucher's percentage discount configuration is invalid.");

                calculatedDiscount = request.OrderAmount * voucher.DiscountPercent.Value / 100m;

                if (voucher.MaxDiscountAmount.HasValue)
                {
                    calculatedDiscount = Math.Min(calculatedDiscount, voucher.MaxDiscountAmount.Value);
                }
            }
            else if (voucher.DiscountType == "FIXED")
            {
                if (!voucher.DiscountAmount.HasValue)
                    return Invalid("The voucher's fixed discount configuration is invalid.");

                calculatedDiscount = voucher.DiscountAmount.Value;
            }
            else
            {
                return Invalid("Invalid voucher discount type.");
            }

            var discountAmount = (long)Math.Round(calculatedDiscount, MidpointRounding.AwayFromZero);

            discountAmount = Math.Clamp(discountAmount, 0L, request.OrderAmount);

            return new ApplyVoucherResponseDTO
            {
                IsValid = true,
                Code = voucher.Code,
                DiscountAmount = discountAmount,
                FinalAmount = request.OrderAmount - discountAmount,
                Message = "Voucher applied successfully.",
                VoucherId = voucher.VoucherId
            };
        }

        public async Task<List<VoucherResponseDTO>> GetAvailableForUserAsync(
            int eventId,
            int userId)
        {
            if (eventId <= 0)
                throw new ArgumentException("Invalid event.");

            var eventOrganizerId =
                await _eventApiClient.GetOrganizerIdByEventIdAsync(eventId);

            var vouchers = await _voucherRepository.GetAvailableForUserAsync(
                eventId,
                eventOrganizerId,
                userId,
                DateTime.UtcNow);

            return vouchers
                .Select(MapToResponseDTO)
                .ToList();
        }

        public async Task RecordUsageForOrderAsync(int orderId)
        {
            var orderInfo = await _ticketApiClient.GetOrderVoucherInfoAsync(orderId);

            if (orderInfo == null || !orderInfo.VoucherId.HasValue)
                return;

            await _voucherRepository.RecordUsageAsync(new VoucherUsage
            {
                OrderId = orderId,
                VoucherId = orderInfo.VoucherId.Value,
                UserId = orderInfo.UserId,
                DiscountAmount = orderInfo.DiscountAmount,
                UsedAt = DateTime.UtcNow
            });
        }
    }
}
