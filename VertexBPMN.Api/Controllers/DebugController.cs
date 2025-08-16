using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Services;

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

        // Fix: Create an ILoggerFactory instance before using CreateLogger
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<TokenEngine>();

        var engine = new TokenEngine(logger, new ServiceTaskRegistry());
        var trace = engine.Execute(model);
        return Ok(trace);
    }

    public record TraceRequest(string BpmnXml, IDictionary<string, object>? Variables);
}
