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
        [HttpPost]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherDTO dto)
        {
            try
            {
                var result = await _voucherService.CreateVoucherAsync(dto, CurrentUserId, IsAdmin, IsOrganizer);
                return StatusCode(201, ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Tạo voucher thành công!"));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Lay danh sach voucher co phan trang va bo loc danh cho Admin va Organizer
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
                return Ok(ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Cập nhật trạng thái voucher thành công."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Cap nhat thong tin voucher he thong hoac voucher cua ban to chuc
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateVoucher(int id, [FromBody] UpdateVoucherDTO dto)
        {
            try
            {
                var result = await _voucherService.UpdateVoucherAsync(id, dto, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<VoucherResponseDTO>.SuccessResponse(result, "Cập nhật voucher thành công."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Xoa mem voucher khoi he thong bang cach danh dau IsDeleted
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            try
            {
                await _voucherService.SoftDeleteVoucherAsync(id, CurrentUserId, IsAdmin, IsOrganizer);
                return Ok(ApiResponseDTO<object>.SuccessResponse(new { id }, "Xóa voucher thành công."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableForUser([FromQuery(Name = "event_id")] int eventId)
        {
            try
            {
                var vouchers = await _voucherService.GetAvailableForUserAsync(eventId, CurrentUserId);

                return Ok(ApiResponseDTO<List<VoucherResponseDTO>>.SuccessResponse(
                        vouchers,
                        "Lấy danh sách voucher khả dụng thành công."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

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
