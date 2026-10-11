using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Migration;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationPreviewRequestDto retained for exposed HTTP action signatures and controller activation (MigrationPreviewRequestDto).")]
public sealed class MigrationPreviewRequestDto
{
	public string? SourceProcessDefinitionId { get; set; }
	public string? TargetProcessDefinitionId { get; set; }
}
