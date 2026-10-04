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

        /// <summary>
        /// Tạo Voucher mới (Admin tạo SYSTEM/ORGANIZER/EVENT; Organizer tạo ORGANIZER/EVENT).
        /// </summary>
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

        /// <summary>
        /// Lấy danh sách Voucher có phân trang và bộ lọc.
        /// Role Admin: Thấy toàn bộ voucher.
        /// Role Organizer: Chỉ thấy voucher do mình tạo hoặc sở hữu.
        /// </summary>
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

        /// <summary>
        /// Xem chi tiết Voucher theo ID.
        /// </summary>
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

        /// <summary>
        /// Xem lịch sử đơn hàng đã sử dụng Voucher (Audit Log).
        /// </summary>
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

        /// <summary>
        /// Bật/Tắt trạng thái hoạt động của Voucher (is_active).
        /// </summary>
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

        /// <summary>
        /// Cập nhật thông tin Voucher (UC_44 / UC_44.2).
        /// </summary>
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

        /// <summary>
        /// Xóa mềm Voucher (is_deleted = true).
        /// </summary>
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
    }
}
