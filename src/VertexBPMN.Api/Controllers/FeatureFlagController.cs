using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/feature-flags")]
[Authorize(Policy = "ReadOnly")]
public class FeatureFlagController : ControllerBase
{
    private readonly BpmnDbContext _db;

    public FeatureFlagController(BpmnDbContext db) => _db = db;

    /// <summary>
    /// Returns the current state of all feature flags.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var flags = await _db.FeatureFlags.AsNoTracking()
            .ToDictionaryAsync(flag => flag.Name, flag => flag.Enabled, cancellationToken);
        return Ok(flags);
    }

    /// <summary>
    /// Enables or disables a feature flag at runtime (demo, not thread-safe).
    /// </summary>
    [HttpPut("{flag}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetFlag(string flag, [FromBody] bool enabled, CancellationToken cancellationToken)
    {
        var normalizedFlag = flag.ToLowerInvariant();
        var record = await _db.FeatureFlags.FindAsync([normalizedFlag], cancellationToken);
        if (record is null)
            return NotFound();

        record.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
