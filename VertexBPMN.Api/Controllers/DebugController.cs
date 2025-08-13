using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;
using System.Collections.Generic;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly IRuntimeService _runtimeService;

    public DebugController(IRepositoryService repositoryService, IRuntimeService runtimeService)
    {
        _repositoryService = repositoryService;
        _runtimeService = runtimeService;
    }

    // Simulates a BPMN process and returns the execution trace for visual debugging.
    [HttpPost("trace")]
    public ActionResult<List<string>> Trace([FromBody] TraceRequest request)
    {
        var parser = new BpmnParser();
        var model = parser.Parse(request.BpmnXml);
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        return Ok(trace);
    }

    public record TraceRequest(string BpmnXml, IDictionary<string, object>? Variables);
}
