using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;

namespace TicketAPI.Services
{
    public class TicketGovernanceService : ITicketGovernanceService
    {
        private readonly ITicketGovernanceRepository _repository;
        private readonly IEventApiClient _eventApiClient;
        private readonly IStatusHistoryService _statusHistoryService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TicketGovernanceService> _logger;
        private readonly IConnectionMultiplexer? _redis;

        public TicketGovernanceService(
            ITicketGovernanceRepository repository,
            IEventApiClient eventApiClient,
            IStatusHistoryService statusHistoryService,
            IHttpClientFactory httpClientFactory,
            ILogger<TicketGovernanceService> logger,
            IServiceProvider serviceProvider)
        {
            _repository = repository;
            _eventApiClient = eventApiClient;
            _statusHistoryService = statusHistoryService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            try
            {
                _redis = serviceProvider.GetService<IConnectionMultiplexer>();
            }
            catch
            {
                _redis = null;
            }
        }

        public async Task<BulkRevokeResponseDTO> BulkRevokeTicketsAsync(BulkRevokeRequestDTO request, int adminId, string adminName)
        {
            // 1. Validation logic
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.EventId <= 0)
                throw new ArgumentException("Event ID is required for bulk revocation.");

            if (string.IsNullOrWhiteSpace(request.ReasonNote))
                throw new ArgumentException("Revocation reason note is strictly required.");

            _logger.LogInformation("Admin {AdminName} ({AdminId}) initiating bulk revocation for event {EventId}", adminName, adminId, request.EventId);

            // 2. Fetch event information via EventApiClient
            var eventInfo = await _eventApiClient.GetEventByIdAsync(request.EventId);
            string eventTitle = eventInfo?.Title ?? $"Event #{request.EventId}";

            // 3. Get active tickets from repository
            var activeTickets = await _repository.GetActiveTicketsForEventAsync(request.EventId);
            int revokedCount = activeTickets.Count;

            if (revokedCount <= 0)
            {
                throw new InvalidOperationException($"Event '{eventTitle}' has no active tickets to revoke.");
            }

            // 4. Prepare reconciliation and revocation entities
            var now = DateTime.UtcNow;
            var reconciliation = new TicketReconciliation
            {
                EventId = request.EventId,
                EventTitle = eventTitle,
                AdminId = adminId,
                AdminName = adminName,
                TotalTicketsRevoked = revokedCount,
                ReasonType = request.ReasonType,
                Notes = request.ReasonNote.Trim(),
                CreatedAt = now,
                RefundStatus = "QUEUED"
            };

            var revocations = activeTickets.Select(t => new TicketRevocation
            {
                TicketCode = t.TicketCode.ToString(),
                EventId = request.EventId,
                EventTitle = eventTitle,
                RevokedByAdminId = adminId,
                RevokedByName = adminName,
                RevokedAt = now,
                ReasonType = request.ReasonType,
                ReasonNote = request.ReasonNote.Trim(),
                IsBulk = true,
                RefundQueued = true,
                NotificationSent = true,
                SeatOrZone = t.TicketTypeName ?? "",
                CustomerName = t.OwnerName ?? ""
            }).ToList();

            // 5. Execute DB updates and inserts via repository
            int reconciliationId = await _repository.BulkRevokeTicketsInDbAsync(
                request.EventId,
                adminId,
                reconciliation,
                revocations);

            // 6. Record Status History for audit log
            try
            {
                var historyItems = activeTickets.Select(t => new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = t.TicketId,
                    EventId = t.EventId,
                    OrderId = t.OrderId,
                    TicketId = t.TicketId,
                    TicketTypeId = t.TicketTypeId,
                    SeatId = t.SeatId,
                    HoldId = null,
                    CustomerId = t.OwnerUserId ?? 0,
                    Action = "BulkRevoked",
                    OldStatus = "Active",
                    NewStatus = "Revoked",
                    Quantity = 1,
                    ActorUserId = adminId,
                    ActorRole = "Admin",
                    Reason = request.ReasonNote.Trim(),
                    Metadata = "{}"
                }).ToList();

                await _statusHistoryService.RecordRangeAsync(historyItems);
            }
            catch (Exception exHist)
            {
                _logger.LogWarning("Status histories recording skipped: {Message}", exHist.Message);
            }

            // 7. Invalidate Redis Gate Cache
            await InvalidateRedisGateCacheAsync(request.EventId);

            // 8. Gather affected customers and dispatch notifications via NotificationAPI
            var affectedCustomerIds = new HashSet<int>();
            foreach (var t in activeTickets)
            {
                if (t.OwnerUserId.HasValue && t.OwnerUserId.Value > 0)
                    affectedCustomerIds.Add(t.OwnerUserId.Value);
            }

            var orderCustomerIds = await _repository.GetCustomerIdsByEventIdAsync(request.EventId);
            foreach (var cid in orderCustomerIds)
            {
                affectedCustomerIds.Add(cid);
            }

            if (affectedCustomerIds.Count > 0)
            {
                await SendAttendeeNotificationsAsync(
                    request.EventId,
                    eventTitle,
                    request.ReasonType,
                    request.ReasonNote.Trim(),
                    reconciliationId,
                    affectedCustomerIds.ToList());
            }

            return new BulkRevokeResponseDTO
            {
                Success = true,
                ReconciliationId = reconciliationId,
                EventId = request.EventId,
                EventTitle = eventTitle,
                TotalRevokedTickets = revokedCount,
                ReasonType = request.ReasonType,
                ReasonNote = request.ReasonNote,
                RevokedAt = now,
                AdminName = adminName,
                GateCacheInvalidated = true,
                RefundQueued = false,
                NotificationSent = true,
                Message = $"Successfully revoked {revokedCount} tickets for event '{eventTitle}'. Reconciliation record #{reconciliationId} created."
            };
        }

        public async Task<RevocationHistoryResponseDTO> GetRevocationHistoryAsync(int? eventId, string? search, string? reasonType, int page, int pageSize)
        {
            var (items, totalCount) = await _repository.GetRevocationHistoryAsync(eventId, search, reasonType, page, pageSize);

            var dtoList = items.Select(r => new RevocationHistoryItemDTO
            {
                RevocationId = r.RevocationId,
                TicketCode = r.TicketCode,
                EventId = r.EventId,
                EventTitle = r.EventTitle,
                RevokedByAdminId = r.RevokedByAdminId,
                RevokedByName = r.RevokedByName,
                RevokedAt = r.RevokedAt,
                ReasonType = r.ReasonType,
                ReasonNote = r.ReasonNote ?? "",
                ReconciliationId = r.ReconciliationId,
                IsBulkRevocation = r.IsBulk,
                RefundQueued = r.RefundQueued,
                NotificationSent = r.NotificationSent,
                SeatOrZone = r.SeatOrZone ?? "",
                CustomerName = r.CustomerName ?? ""
            }).ToList();

            var effPageSize = pageSize <= 0 ? 10 : pageSize;
            return new RevocationHistoryResponseDTO
            {
                Items = dtoList,
                TotalCount = totalCount,
                Page = Math.Max(1, page),
                PageSize = effPageSize,
                TotalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / effPageSize) : 1
            };
        }

        public async Task<ReissueHistoryResponseDTO> GetReissueHistoryAsync(int? eventId, string? search, int page, int pageSize)
        {
            var (items, totalCount) = await _repository.GetReissueHistoryAsync(eventId, search, page, pageSize);

            var dtoList = items.Select(r => new ReissueHistoryItemDTO
            {
                ReissueId = r.ReissueId,
                OldTicketCode = r.OldTicketCode,
                NewTicketCode = r.NewTicketCode,
                EventId = r.EventId,
                EventTitle = r.EventTitle,
                ReissuedByAdminId = r.ReissuedByAdminId,
                ReissuedByName = r.ReissuedByName,
                ReissuedAt = r.ReissuedAt,
                Reason = r.Reason,
                CustomerName = r.CustomerName,
                CustomerEmail = r.CustomerEmail,
                SeatOrZone = r.SeatOrZone ?? "",
                DynamicQrStatus = r.DynamicQrStatus ?? "ACTIVE"
            }).ToList();

            var effPageSize = pageSize <= 0 ? 10 : pageSize;
            return new ReissueHistoryResponseDTO
            {
                Items = dtoList,
                TotalCount = totalCount,
                Page = Math.Max(1, page),
                PageSize = effPageSize,
                TotalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / effPageSize) : 1
            };
        }

        public async Task<List<ReconciliationRecordDTO>> GetReconciliationRecordsAsync(int? eventId)
        {
            var records = await _repository.GetReconciliationRecordsAsync(eventId);

            return records.Select(r => new ReconciliationRecordDTO
            {
                ReconciliationId = r.ReconciliationId,
                EventId = r.EventId,
                EventTitle = r.EventTitle,
                AdminId = r.AdminId,
                AdminName = r.AdminName,
                TotalTicketsRevoked = r.TotalTicketsRevoked,
                ReasonType = r.ReasonType,
                Notes = r.Notes ?? "",
                CreatedAt = r.CreatedAt,
                RefundStatus = r.RefundStatus
            }).ToList();
        }

        public async Task<GovernanceStatsDTO> GetGovernanceStatsAsync()
        {
            var (bulkEvents, bulkTickets, singleRevocations, reissues, refundQueued) =
                await _repository.GetGovernanceStatsCountsAsync();

            return new GovernanceStatsDTO
            {
                TotalRevokedTickets = bulkTickets + singleRevocations,
                TotalBulkEvents = bulkEvents,
                TotalReissuedTickets = reissues,
                TotalRefundQueued = refundQueued
            };
        }

        public async Task<List<EventSelectionDTO>> GetEventsForGovernanceAsync()
        {
            // 1. Fetch disrupted candidate events from EventAPI
            var candidateEvents = await _eventApiClient.GetDisruptedEventsForGovernanceAsync();

            if (candidateEvents.Count == 0)
                return new List<EventSelectionDTO>();

            // 2. Fetch live active tickets count from TicketAPI DB via EF Core
            var eventIds = candidateEvents.Select(e => e.EventId).Distinct().ToList();
            var counts = await _repository.GetActiveTicketCountsByEventIdsAsync(eventIds);

            // 3. Match counts to candidate events
            foreach (var ev in candidateEvents)
            {
                ev.ActiveTicketsCount = counts.TryGetValue(ev.EventId, out int cnt) ? cnt : 0;
            }

            // 4. Return only events that currently have active tickets that need revocation
            return candidateEvents.Where(e => e.ActiveTicketsCount > 0).ToList();
        }

        private async Task InvalidateRedisGateCacheAsync(int eventId)
        {
            try
            {
                if (_redis != null && _redis.IsConnected)
                {
                    var db = _redis.GetDatabase();
                    await db.StringSetAsync($"event:revoked:{eventId}", "REVOKED", TimeSpan.FromDays(30));
                    await db.KeyDeleteAsync($"gate:event:{eventId}:manifest");
                    await db.KeyDeleteAsync($"gate:event:{eventId}:tickets");
                    _logger.LogInformation("Redis cache: Set event:revoked:{EventId} = REVOKED and flushed gate manifest.", eventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Notice: Redis flush failure: {Message}", ex.Message);
            }
        }

        private async Task SendAttendeeNotificationsAsync(
            int eventId,
            string eventTitle,
            string reasonType,
            string reasonNote,
            int reconciliationId,
            List<int> customerIds)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ChangeNotification");
                string notifTitle = reasonType == "EVENT_CANCELLED"
                    ? $"Event Cancellation Notice: {eventTitle}"
                    : $"Event Reschedule Notice: {eventTitle}";
                string notifContent = $"Event '{eventTitle}' has been {(reasonType == "EVENT_CANCELLED" ? "cancelled" : "postponed")}. Reason: {reasonNote}. Your tickets for this event have been revoked (Reconciliation #{reconciliationId}).";

                foreach (var uid in customerIds)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/notifications");
                        request.Headers.Add("Idempotency-Key", $"bulk-revoke-{eventId}-{reconciliationId}-{uid}");
                        request.Content = JsonContent.Create(new
                        {
                            userId = uid,
                            title = notifTitle,
                            message = notifContent,
                            category = "ticket_revocation",
                            targetUrl = "/my-tickets"
                        });

                        using var response = await client.SendAsync(request);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Notification sending for user {UserId} returned status {Status}", uid, response.StatusCode);
                        }
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogWarning(exItem, "Failed to send notification to user {UserId}", uid);
                    }
                }

                _logger.LogInformation("Dispatched disruption notifications to {Count} customers for event {EventId}", customerIds.Count, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send disruption notifications for event {EventId}", eventId);
            }
        }
    }
}
