using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IManagementService _managementService;

    public MetricsController(IManagementService managementService)
    {
        _managementService = managementService;
    }

    /// <summary>
    /// Returns engine metrics for Prometheus/OpenTelemetry scraping.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> Get()
    {
        var metrics = await _managementService.GetMetricsAsync();
        return Ok(metrics);
    }

    /// <summary>
    /// Returns engine metrics in Prometheus text format.
    /// </summary>
    [HttpGet("prometheus")]
    [Produces("text/plain")]
    public async Task<IActionResult> GetPrometheus()
    {
        var metrics = await _managementService.GetMetricsAsync();
        var lines = metrics.Select(kv => $"vertexbpmn_{kv.Key} {kv.Value}");
        return Content(string.Join("\n", lines), "text/plain");
    }
}
