using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Configuration;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/engine")]
[Authorize]
public sealed class EngineController : ControllerBase
{
    private readonly IProcessEngine _engine;
    private readonly AdvancedFeatureOptions _features;

    public EngineController(IProcessEngine engine, IOptions<AdvancedFeatureOptions> features)
    {
        _engine = engine;
        _features = features.Value;
    }

    [HttpGet("capabilities")]
    public ActionResult<EngineCapabilities> GetCapabilities()
    {
        var isDistributed = _engine is IDistributedProcessEngine;
        return Ok(new EngineCapabilities(
            isDistributed ? ProcessEngineType.Distributed : ProcessEngineType.Simple,
            SupportsCmmn: _features.CmmnExecution,
            SupportsWorkers: isDistributed,
            SupportsDurablePersistence: isDistributed));
    }

    public sealed record EngineCapabilities(
        ProcessEngineType EngineType,
        bool SupportsCmmn,
        bool SupportsWorkers,
        bool SupportsDurablePersistence);
}
