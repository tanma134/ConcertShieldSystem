using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly IVoucherRepository _voucherRepository;

        public VoucherService(IVoucherRepository voucherRepository)
        {
            _voucherRepository = voucherRepository;
        }

        public async Task<VoucherResponseDTO> CreateVoucherAsync(CreateVoucherDTO dto, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new ArgumentException("Mã voucher không được để trống.");
            }

            var trimmedCode = dto.Code.Trim();
            if (trimmedCode.Contains(' '))
            {
                throw new ArgumentException("Mã voucher không được chứa khoảng trắng.");
            }

            var normalizedCode = trimmedCode.ToUpperInvariant();

            if (await _voucherRepository.CodeExistsAsync(normalizedCode))
            {
                throw new InvalidOperationException($"Mã voucher '{normalizedCode}' đã tồn tại trong hệ thống.");
            }

            var scope = (dto.Scope ?? "").Trim().ToUpperInvariant();
            if (scope != "SYSTEM" && scope != "ORGANIZER" && scope != "EVENT")
            {
                throw new ArgumentException("Scope không hợp lệ. Chỉ chấp nhận SYSTEM, ORGANIZER hoặc EVENT.");
            }

            int? organizerId = dto.OrganizerId;
            int? eventId = dto.EventId;

            if (scope == "SYSTEM")
            {
                if (!isAdmin)
                {
                    throw new UnauthorizedAccessException("Chỉ Quản trị viên (Admin) mới có quyền tạo Voucher toàn hệ thống (SYSTEM).");
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
                    throw new ArgumentException("event_id là bắt buộc khi tạo Voucher theo Sự kiện (EVENT).");
                }
                if (!organizerId.HasValue || organizerId.Value <= 0)
                {
                    organizerId = currentUserId;
                }
            }

            var discountType = (dto.DiscountType ?? "").Trim().ToUpperInvariant();
            if (discountType != "PERCENT" && discountType != "FIXED")
            {
                throw new ArgumentException("Loại giảm giá (discount_type) không hợp lệ. Chỉ chấp nhận PERCENT hoặc FIXED.");
            }

            long? discountAmount = null;
            decimal? discountPercent = null;
            long? maxDiscountAmount = null;

            if (discountType == "PERCENT")
            {
                if (!dto.DiscountPercent.HasValue || dto.DiscountPercent.Value <= 0 || dto.DiscountPercent.Value > 100)
                {
                    throw new ArgumentException("Phần trăm giảm giá phải lớn hơn 0 và không vượt quá 100%.");
                }
                discountPercent = dto.DiscountPercent.Value;
                maxDiscountAmount = dto.MaxDiscountAmount.HasValue && dto.MaxDiscountAmount.Value > 0 
                    ? dto.MaxDiscountAmount.Value 
                    : null;
            }
            else // FIXED
            {
                if (!dto.DiscountAmount.HasValue || dto.DiscountAmount.Value <= 0)
                {
                    throw new ArgumentException("Số tiền giảm giá cố định phải lớn hơn 0.");
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
                throw new ArgumentException("Thời gian bắt đầu (starts_at) phải trước thời gian kết thúc (ends_at).");
            }

            if (dto.TotalQuantity <= 0)
            {
                throw new ArgumentException("Tổng số lượng voucher phải lớn hơn 0.");
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

                // 1. Direct persistence to notification_db (100% resilient)
                try
                {
                    using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
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
                    // Fallback to HTTP
                }

                // 2. HTTP Realtime ping to NotificationAPI
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

                    var endpoints = new[]
                    {
                        "https://localhost:7197/api/notifications",
                        "http://localhost:7186/api/notifications"
                    };

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
                throw new UnauthorizedAccessException("Bạn không có quyền xem danh sách voucher.");
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
                throw new KeyNotFoundException($"Không tìm thấy voucher với ID {id}.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền truy cập voucher này.");
                }
            }

            return MapToResponseDTO(voucher);
        }

        public async Task<List<VoucherUsageDTO>> GetVoucherUsagesAsync(int voucherId, int currentUserId, bool isAdmin, bool isOrganizer)
        {
            var voucher = await _voucherRepository.GetByIdAsync(voucherId);
            if (voucher == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy voucher với ID {voucherId}.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền xem lịch sử sử dụng của voucher này.");
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
                throw new KeyNotFoundException($"Không tìm thấy voucher với ID {id}.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền cập nhật trạng thái voucher này.");
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

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to update this voucher.");
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

            // 2. Total quantity constraint: total_quantity >= used_quantity
            if (dto.TotalQuantity.HasValue)
            {
                if (dto.TotalQuantity.Value < voucher.UsedQuantity)
                {
                    throw new ArgumentException($"Total quantity ({dto.TotalQuantity.Value}) cannot be less than used quantity ({voucher.UsedQuantity}).");
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

            // ends_at shortening constraint: New ends_at must be >= now. (To deactivate immediately, set is_active to false)
            if (dto.EndsAt.HasValue && newEndsAt < now)
            {
                throw new InvalidOperationException("New end date cannot be in the past. To deactivate voucher immediately, set status to Inactive.");
            }

            voucher.StartsAt = newStartsAt;
            voucher.EndsAt = newEndsAt;

            if (dto.DiscountAmount.HasValue) voucher.DiscountAmount = dto.DiscountAmount.Value;
            if (dto.DiscountPercent.HasValue) voucher.DiscountPercent = dto.DiscountPercent.Value;
            if (dto.MaxDiscountAmount.HasValue) voucher.MaxDiscountAmount = dto.MaxDiscountAmount.Value;
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
                throw new KeyNotFoundException($"Không tìm thấy voucher với ID {id}.");
            }

            if (!isAdmin && isOrganizer)
            {
                if (voucher.OrganizerId != currentUserId && voucher.CreatedBy != currentUserId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền xóa voucher này.");
                }
            }

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
    }
}
