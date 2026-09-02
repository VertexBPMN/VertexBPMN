using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Configuration;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/engine")]
[Authorize]
public sealed class EngineController : ControllerBase
{
    private readonly IProcessEngine _engine;
    public EngineController(IProcessEngine engine) => _engine = engine;

    [HttpGet("capabilities")]
    public ActionResult<EngineCapabilities> GetCapabilities()
    {
        var isDistributed = _engine is IDistributedProcessEngine;
        return Ok(new EngineCapabilities(
            (isDistributed ? ProcessEngineType.Distributed : ProcessEngineType.Simple).ToString(),
            SupportsCmmn: true,
            SupportsWorkers: isDistributed,
            SupportsDurablePersistence: isDistributed));
    }

    public sealed record EngineCapabilities(
        string EngineType,
        bool SupportsCmmn,
        bool SupportsWorkers,
        bool SupportsDurablePersistence);
}
