using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [ApiController]
    [Route("api/checkin")]
    public class CheckinController : TicketApiControllerBase
    {
        private readonly ICheckinService _checkinService;

        public CheckinController(ICheckinService checkinService)
        {
            _checkinService = checkinService;
        }

        [HttpPost("scan")]
        [Authorize(Policy = "RequireStaff")]
        public async Task<IActionResult> Scan([FromBody] CheckinScanRequestDto request)
        {
            try
            {
                var result = await _checkinService.ProcessScanAsync(request, CurrentUserId);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        data = result
                    });
                }

                return Success(result, result.Message);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }
    }
}
