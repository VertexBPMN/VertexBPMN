using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/history")]
public class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;

    public HistoryController(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("by-process-instance/{processInstanceId}")]
    public IAsyncEnumerable<HistoryEvent> ListByProcessInstance(Guid processInstanceId)
        => _historyService.ListByProcessInstanceAsync(processInstanceId);

    [HttpGet("{id}")]
    public async Task<ActionResult<HistoryEvent>> GetById(Guid id)
    {
        var evt = await _historyService.GetByIdAsync(id);
        if (evt is null) return NotFound();
        return evt;
    }
}
