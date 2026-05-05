using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Models;
using KKTMonitor.Services;

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly ScheduleService _service;

        public ScheduleController(ScheduleService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var schedules = await _service.GetAll();
                return Ok(schedules);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScheduleController] Error: {ex.Message}");
                return Ok(new List<ScheduleSettings>());
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var schedule = await _service.GetById(id);
            return schedule == null ? NotFound() : Ok(schedule);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ScheduleSettings schedule)
        {
            if (string.IsNullOrWhiteSpace(schedule.ScheduleTime))
                return BadRequest(new { error = "Время не может быть пустым" });
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(schedule.ScheduleTime, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"))
                return BadRequest(new { error = "Неверный формат времени. Используйте HH:MM" });
            
            schedule.CreatedAt = DateTime.Now;
            schedule.UpdatedAt = DateTime.Now;
            schedule.IsActive = true;
            
            int id = await _service.Create(schedule);
            var created = await _service.GetById(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ScheduleSettings schedule)
        {
            if (id != schedule.Id) return BadRequest(new { error = "ID mismatch" });
            
            if (string.IsNullOrWhiteSpace(schedule.ScheduleTime))
                return BadRequest(new { error = "Время не может быть пустым" });
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(schedule.ScheduleTime, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"))
                return BadRequest(new { error = "Неверный формат времени. Используйте HH:MM" });
            
            schedule.UpdatedAt = DateTime.Now;
            bool updated = await _service.Update(schedule);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted = await _service.Delete(id);
            return deleted ? Ok() : NotFound();
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleActive(int id, [FromQuery] bool isActive)
        {
            bool updated = await _service.ToggleActive(id, isActive);
            return updated ? Ok() : NotFound();
        }
    }
}