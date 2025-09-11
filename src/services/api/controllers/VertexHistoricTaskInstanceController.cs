using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Domain;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/history/task")]
[Authorize]
public class VertexHistoricTaskInstanceController : ControllerBase
{
    private readonly IHistoryService _historyService;

    public VertexHistoricTaskInstanceController(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet]
    public async IAsyncEnumerable<HistoricTaskInstanceDto> GetAll([FromQuery] Guid? processInstanceId = null)
    {
        await foreach (var evt in _historyService.ListHistoricTasksAsync(processInstanceId))
            yield return ToDto(evt);
    }

    private static HistoricTaskInstanceDto ToDto(HistoryEvent e) => new()
    {
        Id = e.Id,
        ProcessInstanceId = e.ProcessInstanceId.ToString(),
        StartTime = e.Timestamp,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
