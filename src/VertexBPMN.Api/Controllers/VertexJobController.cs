using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using CoreJob = VertexBPMN.Domain.Entities.Job;
using Microsoft.AspNetCore.Authorization;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/vertex/job")]
[Authorize(Policy = "TenantReadOnly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class VertexJobController : ControllerBase
{
	private readonly IJobRepository _jobRepository;
	public VertexJobController(IJobRepository jobRepository)
	{
		_jobRepository = jobRepository;
	}

	[HttpGet]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Tenant filtering is performed while streaming IAsyncEnumerable jobs. Enumerable.Where cannot filter this async stream; materializing it would increase memory use and delay responses.")]
	public async IAsyncEnumerable<JobDto> GetAll()
	{
		var currentTenant = CurrentTenantId();
		if (!User.IsInRole("Admin") && string.IsNullOrWhiteSpace(currentTenant))
		{
			yield break;
		}

		await foreach (var job in _jobRepository.ListDueAsync(DateTime.UtcNow.AddYears(100)))
		{
			if (currentTenant is null || string.Equals(job.TenantId, currentTenant, StringComparison.Ordinal))
			{
				yield return ToDto(job);
			}
		}
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<JobDto>> GetById(Guid id)
	{
		if (!User.IsInRole("Admin") && string.IsNullOrWhiteSpace(CurrentTenantId()))
		{
			return Forbid();
		}

		var job = await _jobRepository.GetByIdAsync(id);
		if (job is null)
		{
			return NotFound();
		}

		var currentTenant = CurrentTenantId();
		if (currentTenant is not null && !string.Equals(job.TenantId, currentTenant, StringComparison.Ordinal))
		{
			return NotFound();
		}

		return ToDto(job);
	}

	private string? CurrentTenantId() => User.IsInRole("Admin") ? null : User.FindFirstValue("tenant_id");
	private static JobDto ToDto(CoreJob j) => new()
	{
		Id = j.Id.ToString(),
		ProcessInstanceId = j.ProcessInstanceId.ToString(),
		JobType = j.Type,
		DueDate = j.DueDate,
		Retries = j.Retries,
		ExceptionMessage = j.ErrorMessage ?? string.Empty,
	};
}
