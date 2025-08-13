using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Domain;
using VertexBPMN.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/tenant")]
    public class TenantController : ControllerBase
    {
        private readonly TenantDbContext _db;
        public TenantController(TenantDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Tenant>>> GetAll()
        {
            return Ok(await _db.Tenants.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tenant>> GetById(string id)
        {
            var tenant = await _db.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            return Ok(tenant);
        }

        [HttpPost]
        public async Task<ActionResult<Tenant>> Create([FromBody] Tenant tenant)
        {
            tenant.Id = Guid.NewGuid().ToString();
            tenant.CreatedAt = DateTime.UtcNow;
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] Tenant update)
        {
            var tenant = await _db.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            tenant.Name = update.Name;
            tenant.Description = update.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var tenant = await _db.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
