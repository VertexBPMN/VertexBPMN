using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly IBpmnParser _parser;
    private readonly IProcessEngine _processEngine;
    private readonly IRuntimeService _runtimeService;

    public DebugController(IRepositoryService repositoryService, IBpmnParser parser, IProcessEngine processEngine, IRuntimeService runtimeService)
    {
        _repositoryService = repositoryService;
        _parser = parser;
        _processEngine = processEngine;
        _runtimeService = runtimeService;
    }

    // Simulates a BPMN process and returns the execution trace for visual debugging.
    [HttpPost("trace")]
    public async Task<ActionResult<List<string>>> Trace([FromBody] TraceRequest request)
    {

        var model = await _parser.ParseAsync(request.BpmnXml);
        var trace = _processEngine.Execute(model);
        return Ok(trace);
    }

    public record TraceRequest(string BpmnXml, IDictionary<string, object>? Variables);
}
