using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Infrastructure.Features;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/feature-flags")]
public class FeatureFlagController : ControllerBase
{
    /// <summary>
    /// Returns the current state of all feature flags.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new
        {
            LiveInspector = FeatureFlags.LiveInspector,
            PredictiveAnalytics = FeatureFlags.PredictiveAnalytics,
            ProcessMiningApi = FeatureFlags.ProcessMiningApi
        });
    }

    /// <summary>
    /// Enables or disables a feature flag at runtime (demo, not thread-safe).
    /// </summary>
    [HttpPut("{flag}")]
    public IActionResult SetFlag(string flag, [FromBody] bool enabled)
    {
        switch (flag.ToLowerInvariant())
        {
            case "liveinspector": FeatureFlags.LiveInspector = enabled; break;
            case "predictiveanalytics": FeatureFlags.PredictiveAnalytics = enabled; break;
            case "processminingapi": FeatureFlags.ProcessMiningApi = enabled; break;
            default: return NotFound();
        }
        return NoContent();
    }
}
