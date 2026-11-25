using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [Route("api/v1/vouchers")]
    [Route("api/vouchers")]
    [Authorize]
    public class VouchersController : BaseApiController
    {
        private readonly IVoucherService _voucherService;

        public VouchersController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        // Tao voucher giam gia moi danh cho Admin va Organizer
        [Authorize(Roles = "Organizer,Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherDTO dto)
        {
            try
            {
                var result = await _voucherService.CreateVoucherAsync(dto, CurrentUserId, IsAdmin, IsOrganizer);
                return StatusCode(201, ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Voucher created successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Lay danh sach voucher co phan trang va bo loc danh cho Admin va Organizer
        [Authorize(Roles = "Organizer,Admin")]
        [HttpGet]
        public async Task<IActionResult> GetVouchers(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] string? scope = null,
            [FromQuery(Name = "is_active")] bool? isActive = null,
            [FromQuery(Name = "search_code")] string? searchCode = null,
            [FromQuery(Name = "organizer_id")] int? organizerId = null)
        {
            try
            {
                var result = await _voucherService.GetVouchersAsync(
                    page,
                    limit,
                    scope,
                    isActive,
                    searchCode,
                    organizerId,
                    CurrentUserId,
                    IsAdmin,
                    IsOrganizer);

                return Ok(ApiResponseDTO<PagedResultDTO<VoucherResponseDTO>>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Xem thong tin chi tiet voucher theo ma dinh danh danh cho Admin va Organizer
        [Authorize(Roles = "Organizer,Admin")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetVoucherById(int id)
        {
            try
            {
                var result = await _voucherService.GetVoucherByIdAsync(id, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Xem lich su cac don hang da su dung voucher danh cho Admin va Organizer
        [Authorize(Roles = "Organizer,Admin")]
        [HttpGet("{id:int}/usages")]
        public async Task<IActionResult> GetVoucherUsages(int id)
        {
            try
            {
                var result = await _voucherService.GetVoucherUsagesAsync(id, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<List<VoucherUsageDTO>>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Bat hoac tat trang thai kich hoat cua voucher danh cho Admin va Organizer
        [Authorize(Roles = "Organizer,Admin")]
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateVoucherStatus(int id, [FromBody] UpdateVoucherStatusDTO? dto)
        {
            try
            {
                bool newStatus;
                if (dto != null)
                {
                    newStatus = dto.IsActive;
                }
                else
                {
                    // If no body provided, toggle current status
                    var current = await _voucherService.GetVoucherByIdAsync(id, CurrentUserId, IsAdmin, IsOrganizer);
                    newStatus = !current.IsActive;
                }

                var result = await _voucherService.UpdateVoucherStatusAsync(id, newStatus, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Voucher status updated successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Cap nhat thong tin voucher he thong hoac voucher cua ban to chuc
        [Authorize(Roles = "Organizer,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateVoucher(int id, [FromBody] UpdateVoucherDTO dto)
        {
            try
            {
                var result = await _voucherService.UpdateVoucherAsync(id, dto, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Voucher updated successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Xoa mem voucher khoi he thong bang cach danh dau IsDeleted
        [Authorize(Roles = "Organizer,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            try
            {
                await _voucherService.SoftDeleteVoucherAsync(id, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<object>.SuccessResponse(new { id }, "Voucher marked as deleted successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableForUser([FromQuery(Name = "event_id")] int eventId)
        {
            try
            {
                var vouchers = await _voucherService.GetAvailableForUserAsync(eventId, CurrentUserId);

                return Ok(ApiResponseDTO<List<VoucherResponseDTO>>.SuccessResponse(
                        vouchers,
                        "Available vouchers retrieved successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyVoucher(
            [FromBody] ApplyVoucherRequestDTO request)
        {
            try
            {
                var result = await _voucherService.ApplyVoucherAsync(request, CurrentUserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}
