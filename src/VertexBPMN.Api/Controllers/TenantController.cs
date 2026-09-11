using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/tenant")]
    [Authorize(Policy = "ReadOnly")]
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
            if (User.IsInRole("Admin"))
                return Ok(await _db.Tenants.ToListAsync());

            var tenantId = User.FindFirstValue("tenant_id");
            if (string.IsNullOrWhiteSpace(tenantId))
                return Forbid();

            return Ok(await _db.Tenants.Where(tenant => tenant.Id == tenantId).ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tenant>> GetById(string id)
        {
            if (!User.IsInRole("Admin")
                && !string.Equals(User.FindFirstValue("tenant_id"), id, StringComparison.Ordinal))
            {
                return Forbid();
            }

            var tenant = await _db.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            return Ok(tenant);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<Tenant>> Create([FromBody] Tenant tenant)
        {
            tenant.Id = Guid.NewGuid().ToString();
            tenant.CreatedAt = DateTime.UtcNow;
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
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
        [Authorize(Policy = "AdminOnly")]
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
