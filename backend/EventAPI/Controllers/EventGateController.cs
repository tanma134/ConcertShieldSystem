using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Controllers
{
    [ApiController]
    [Route("api/events/{eventId:int}/gates")]
    public class EventGateController : BaseApiController
    {
        private readonly EventDbContext _context;
        private readonly IEventRepository _events;

        public EventGateController(EventDbContext context, IEventRepository events)
        {
            _context = context;
            _events = events;
        }

        private async Task EnsureEventGatesTableAsync()
        {
            try
            {
                var sql = @"
                    CREATE TABLE IF NOT EXISTS public.event_gates (
                        gate_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        event_id integer NOT NULL,
                        gate_name varchar(100) NOT NULL,
                        description varchar(255) NULL,
                        allowed_ticket_types text NULL,
                        created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IF NOT EXISTS ix_event_gates_event_id ON public.event_gates(event_id);
                ";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch
            {
                // Ignore if already created or permission issue
            }
        }

        private async Task<Event> EnsureOwnerOrAdminAsync(int eventId)
        {
            var ev = await _events.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event #{eventId} not found.");

            if (!IsAdmin && ev.OrganizerId != CurrentUserId)
                throw new KeyNotFoundException($"Event #{eventId} not found.");

            return ev;
        }

        private static EventGateDTO ToDto(EventGate gate)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(gate.AllowedTicketTypes))
            {
                list = gate.AllowedTicketTypes
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return new EventGateDTO
            {
                GateId = gate.GateId,
                EventId = gate.EventId,
                GateName = gate.GateName,
                Description = gate.Description,
                AllowedTicketTypes = gate.AllowedTicketTypes,
                AllowedTicketTypeList = list,
                CreatedAt = gate.CreatedAt
            };
        }

        /// <summary>
        /// GET api/events/{eventId}/gates
        /// Lấy danh sách cổng của sự kiện kèm loại vé được phân công
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetGates(int eventId)
        {
            await EnsureEventGatesTableAsync();

            var gates = await _context.EventGates
                .AsNoTracking()
                .Where(g => g.EventId == eventId)
                .OrderBy(g => g.GateName)
                .ThenBy(g => g.GateId)
                .ToListAsync();

            var dtos = gates.Select(ToDto).ToList();
            return Ok(ApiResponseDTO<List<EventGateDTO>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// POST api/events/{eventId}/gates
        /// Tạo cổng mới cho sự kiện và gán loại vé tương ứng
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> CreateGate(int eventId, [FromBody] CreateEventGateDTO dto)
        {
            try
            {
                await EnsureOwnerOrAdminAsync(eventId);
                await EnsureEventGatesTableAsync();

                if (string.IsNullOrWhiteSpace(dto.GateName))
                    return BadRequest(ApiResponseDTO<object>.FailResponse("Gate name cannot be empty."));

                var trimmedName = dto.GateName.Trim();
                var duplicateExists = await _context.EventGates.AnyAsync(g =>
                    g.EventId == eventId &&
                    g.GateName.ToLower() == trimmedName.ToLower());
                if (duplicateExists)
                    return BadRequest(ApiResponseDTO<object>.FailResponse($"A gate with the name '{trimmedName}' already exists for this event."));

                // Chuyển AllowedTicketTypeList sang chuỗi comma-separated nếu có
                string? allowedTypes = dto.AllowedTicketTypes?.Trim();
                if (dto.AllowedTicketTypeList != null && dto.AllowedTicketTypeList.Count > 0)
                {
                    allowedTypes = string.Join(",", dto.AllowedTicketTypeList.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
                }

                var gate = new EventGate
                {
                    EventId = eventId,
                    GateName = trimmedName,
                    Description = dto.Description?.Trim(),
                    AllowedTicketTypes = allowedTypes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EventGates.Add(gate);
                await _context.SaveChangesAsync();

                return Ok(ApiResponseDTO<EventGateDTO>.SuccessResponse(ToDto(gate), "Gate created successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        /// <summary>
        /// PUT api/events/{eventId}/gates/{gateId}
        /// Cập nhật thông tin cổng và loại vé phân công
        /// </summary>
        [HttpPut("{gateId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> UpdateGate(int eventId, int gateId, [FromBody] UpdateEventGateDTO dto)
        {
            try
            {
                await EnsureOwnerOrAdminAsync(eventId);
                await EnsureEventGatesTableAsync();

                var gate = await _context.EventGates.FirstOrDefaultAsync(g => g.EventId == eventId && g.GateId == gateId);
                if (gate == null)
                    return NotFound(ApiResponseDTO<object>.FailResponse($"Gate #{gateId} not found in this event."));

                if (!string.IsNullOrWhiteSpace(dto.GateName))
                {
                    var trimmedName = dto.GateName.Trim();
                    var duplicateExists = await _context.EventGates.AnyAsync(g =>
                        g.EventId == eventId &&
                        g.GateId != gateId &&
                        g.GateName.ToLower() == trimmedName.ToLower());
                    if (duplicateExists)
                        return BadRequest(ApiResponseDTO<object>.FailResponse($"A gate with the name '{trimmedName}' already exists for this event."));

                    gate.GateName = trimmedName;
                }

                if (dto.Description != null)
                    gate.Description = dto.Description.Trim();

                if (dto.AllowedTicketTypeList != null && dto.AllowedTicketTypeList.Count > 0)
                {
                    gate.AllowedTicketTypes = string.Join(",", dto.AllowedTicketTypeList.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
                }
                else if (dto.AllowedTicketTypes != null)
                {
                    gate.AllowedTicketTypes = dto.AllowedTicketTypes.Trim();
                }

                await _context.SaveChangesAsync();
                return Ok(ApiResponseDTO<EventGateDTO>.SuccessResponse(ToDto(gate), "Gate updated successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        /// <summary>
        /// DELETE api/events/{eventId}/gates/{gateId}
        /// Xóa cổng của sự kiện
        /// </summary>
        [HttpDelete("{gateId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> DeleteGate(int eventId, int gateId)
        {
            try
            {
                await EnsureOwnerOrAdminAsync(eventId);
                await EnsureEventGatesTableAsync();

                var gate = await _context.EventGates.FirstOrDefaultAsync(g => g.EventId == eventId && g.GateId == gateId);
                if (gate == null)
                    return NotFound(ApiResponseDTO<object>.FailResponse($"Gate #{gateId} not found in this event."));

                _context.EventGates.Remove(gate);
                await _context.SaveChangesAsync();

                return Ok(ApiResponseDTO<object>.SuccessResponse(new { gateId, eventId }, "Gate deleted successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}
