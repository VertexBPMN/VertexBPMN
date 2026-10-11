using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Migration;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type CreateMigrationPlanRequest retained for exposed HTTP action signatures and controller activation (CreateMigrationPlanRequest).")]
public class CreateMigrationPlanRequest
{
	public Guid? FromProcessDefinitionId { get; set; }
	public Guid? ToProcessDefinitionId { get; set; }
	public string? TenantId { get; set; }
	public string FromProcessKey { get; set; } = string.Empty;
	public string ToProcessKey { get; set; } = string.Empty;
	public MigrationOptions Options { get; set; } = new();
}
