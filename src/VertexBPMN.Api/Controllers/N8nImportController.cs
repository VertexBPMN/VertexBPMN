using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Application.Import;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/import/n8n")]
[Authorize(Policy = "ProcessManager")]
public sealed class N8nImportController(IN8nWorkflowImporter importer) : ControllerBase
{
    [HttpPost]
    public ActionResult<N8nImportResult> Import([FromBody] N8nImportRequest request)
    {
        try { return Ok(importer.Import(request.WorkflowJson)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (System.Text.Json.JsonException exception) { return BadRequest(new ProblemDetails { Detail = $"Invalid n8n JSON: {exception.Message}" }); }
    }

    public sealed record N8nImportRequest(string WorkflowJson);
}
