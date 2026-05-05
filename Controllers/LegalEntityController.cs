using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Models;
using KKTMonitor.Services;

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LegalEntityController : ControllerBase
    {
        private readonly LegalEntityService _service;
        public LegalEntityController(LegalEntityService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAll());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetById(id);
            return entity == null ? NotFound() : Ok(entity);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LegalEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) || string.IsNullOrWhiteSpace(entity.INN))
                return BadRequest("Name and INN are required");
            int id = await _service.Create(entity);
            return CreatedAtAction(nameof(GetById), new { id }, entity);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] LegalEntity entity)
        {
            if (id != entity.Id) return BadRequest("ID mismatch");
            bool updated = await _service.Update(entity);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted = await _service.Delete(id);
            return deleted ? Ok() : NotFound();
        }
    }
}