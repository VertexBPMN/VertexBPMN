using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class DebugController : ControllerBase
{
    private readonly IBpmnParser _parser;
    private readonly IProcessEngine _processEngine;

    public DebugController(IRepositoryService repositoryService, IBpmnParser parser, IProcessEngine processEngine, IRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        _parser = parser;
        _processEngine = processEngine;
        ArgumentNullException.ThrowIfNull(runtimeService);
    }

    // Simulates a BPMN process and returns the execution trace for visual debugging.
    [HttpPost("trace")]
    public async Task<ActionResult<List<string>>> Trace([FromBody] TraceRequest request)
    {

        var model = await _parser.ParseAsync(request.BpmnXml, HttpContext.RequestAborted);
        var trace = await _processEngine.ExecuteAsync(model, HttpContext.RequestAborted);
        return Ok(trace);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type TraceRequest retained for exposed HTTP action signatures and controller activation (DebugController).")]
    public record TraceRequest(string BpmnXml, IDictionary<string, object>? Variables);
}
