using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreJob = VertexBPMN.Core.Domain.Job;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/job")]
public class VertexJobController : ControllerBase
{
    private readonly IJobRepository _jobRepository;

    public VertexJobController(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    [HttpGet]
    public async IAsyncEnumerable<JobDto> GetAll()
    {
        await foreach (var job in _jobRepository.ListDueAsync(DateTime.UtcNow.AddYears(100)))
            yield return ToDto(job);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobDto>> GetById(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job is null) return NotFound();
        return ToDto(job);
    }

    private static JobDto ToDto(CoreJob j) => new()
    {
        Id = j.Id.ToString(),
        ProcessInstanceId = j.ProcessInstanceId.ToString(),
        JobType = j.Type,
        DueDate = j.DueDate,
        Retries = j.Retries,
        ExceptionMessage = j.ErrorMessage ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
