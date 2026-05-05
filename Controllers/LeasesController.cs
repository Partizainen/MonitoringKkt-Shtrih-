using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Models;
using KKTMonitor.Services;

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeasesController : ControllerBase
    {
        private readonly KktService _service;
        private readonly IConfiguration _config;

        public LeasesController(KktService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] List<LeaseDto> leases)
        {
            if (leases == null || leases.Count == 0)
                return BadRequest("Empty payload");

            var filter = _config["AppSettings:LeaseFilter"] ?? string.Empty;

            foreach (var lease in leases)
            {
                // Фильтрация по имени (если задан фильтр)
                if (!string.IsNullOrEmpty(filter) && 
                    !string.IsNullOrEmpty(lease.Hostname) && 
                    !lease.Hostname.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(lease.Ip) && !string.IsNullOrEmpty(lease.Source))
                {
                    await _service.UpsertKkt(lease.Ip, lease.Source);
                    Console.WriteLine($"[DEBUG] LeasesController: Upsert KKT {lease.Ip} with source {lease.Source}");
                }
            }

            return Ok(new { status = "processed", count = leases.Count });
        }
    }
}