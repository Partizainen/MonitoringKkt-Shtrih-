using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Services;

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KktDetailsController : ControllerBase
    {
        private readonly KktDriverService _driverService;

        public KktDetailsController(KktDriverService driverService)
        {
            _driverService = driverService;
        }

        [HttpGet("{ip}/networkStatus")]
        public async Task<IActionResult> CheckNetworkStatus(string ip, [FromQuery] int timeoutMs = 2000)
        {
            var isAvailable = await _driverService.CheckNetworkAccessibility(ip, timeoutMs);
            return Ok(new { ip, isAvailable, checkTime = DateTime.Now });
        }

        [HttpGet("ofdStatus")]
        public async Task<IActionResult> CheckOfdStatus([FromQuery] string ofdUrl = "https://www.nalog.gov.ru", [FromQuery] int timeoutMs = 5000)
        {
            var isAvailable = await _driverService.CheckOfdServerAccessibility(ofdUrl, timeoutMs);
            return Ok(new { ofdUrl, isAvailable, checkTime = DateTime.Now });
        }

        [HttpGet("{ip}/firstReceiptDate")]
        public async Task<IActionResult> GetFirstReceiptDate(string ip)
        {
            var date = await _driverService.GetFirstReceiptDate(ip);
            return Ok(new { ip, firstReceiptDate = date });
        }

        [HttpGet("{ip}/firstRegistrationDate")]
        public async Task<IActionResult> GetFirstRegistrationDate(string ip)
        {
            var date = await _driverService.GetFirstRegistrationDate(ip);
            return Ok(new { ip, firstRegistrationDate = date });
        }

        [HttpGet("{ip}/lastReceiptNumber")]
        public async Task<IActionResult> GetLastReceiptNumber(string ip)
        {
            var number = await _driverService.GetLastReceiptNumber(ip);
            return Ok(new { ip, lastReceiptNumber = number });
        }

        [HttpGet("{ip}/firstReceiptDatePlusDays")]
        public async Task<IActionResult> GetFirstReceiptDatePlusDays(string ip, [FromQuery] int daysToAdd)
        {
            var date = await _driverService.GetFirstReceiptDatePlusDays(ip, daysToAdd);
            return Ok(new { ip, firstReceiptDate = date, daysAdded = daysToAdd });
        }

        [HttpGet("{ip}/firmwareVersion")]
        public async Task<IActionResult> GetFirmwareVersion(string ip)
        {
            var version = await _driverService.GetFirmwareVersion(ip);
            return Ok(new { ip, firmwareVersion = version });
        }

        [HttpGet("{ip}/shiftState")]
        public async Task<IActionResult> GetShiftState(string ip)
        {
            var state = await _driverService.GetShiftState(ip);
            return Ok(state);
        }

        [HttpGet("{ip}/remainingFnCapacity")]
        public async Task<IActionResult> GetRemainingFnCapacity(string ip)
        {
            var capacity = await _driverService.GetRemainingFnCapacity(ip);
            return Ok(new { ip, remainingDocuments = capacity });
        }

        [HttpGet("{ip}/errorStatus")]
        public async Task<IActionResult> GetErrorStatus(string ip)
        {
            var status = await _driverService.GetKktErrorStatus(ip);
            return Ok(status);
        }

        [HttpGet("{ip}/allDetails")]
        public async Task<IActionResult> GetAllDetails(string ip)
        {
            var networkStatus = await _driverService.CheckNetworkAccessibility(ip);
            var firstRegistrationDate = await _driverService.GetFirstRegistrationDate(ip);
            var firstReceiptDate = await _driverService.GetFirstReceiptDate(ip);
            var lastReceiptNumber = await _driverService.GetLastReceiptNumber(ip);
            var firmwareVersion = await _driverService.GetFirmwareVersion(ip);
            var shiftState = await _driverService.GetShiftState(ip);
            var remainingCapacity = await _driverService.GetRemainingFnCapacity(ip);
            var errorStatus = await _driverService.GetKktErrorStatus(ip);

            DateTime? firstReceiptPlus450 = null;
            if (firstRegistrationDate.HasValue)
                firstReceiptPlus450 = firstRegistrationDate.Value.AddDays(450);

            return Ok(new
            {
                ip,
                networkStatus,
                firstRegistrationDate,
                firstReceiptDate,
                firstReceiptPlus450,
                lastReceiptNumber,
                firmwareVersion,
                shiftState,
                remainingDocuments = remainingCapacity,
                errorStatus,
                checkTime = DateTime.Now
            });
        }
    }
}