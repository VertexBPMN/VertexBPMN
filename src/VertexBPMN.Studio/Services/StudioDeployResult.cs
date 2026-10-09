using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

/// <summary>Result of a BPMN deploy: the deployed definition plus any one-time webhook secrets.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public sealed record StudioDeployResult(ProcessDefinition Definition, IReadOnlyList<StudioCreatedWebhook> CreatedWebhooks);
