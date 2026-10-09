using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Studio.Controllers;
[ApiController]
[Route("api/[controller]")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "MVC discovers this public API controller.")]
public sealed class ProcessController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;

    public ProcessController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetProcessDefinitions()
    {
        var definitions = await _repositoryService.GetProcessDefinitionsAsync();
        return Ok(definitions);
    }
}
