using Microsoft.AspNetCore.Mvc;

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
            LiveInspector = VertexBPMN.Core.Features.FeatureFlags.LiveInspector,
            PredictiveAnalytics = VertexBPMN.Core.Features.FeatureFlags.PredictiveAnalytics,
            ProcessMiningApi = VertexBPMN.Core.Features.FeatureFlags.ProcessMiningApi
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
            case "liveinspector": VertexBPMN.Core.Features.FeatureFlags.LiveInspector = enabled; break;
            case "predictiveanalytics": VertexBPMN.Core.Features.FeatureFlags.PredictiveAnalytics = enabled; break;
            case "processminingapi": VertexBPMN.Core.Features.FeatureFlags.ProcessMiningApi = enabled; break;
            default: return NotFound();
        }
        return NoContent();
    }
}
