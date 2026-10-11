using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

/// <summary>A webhook trigger created during a BPMN deploy, with its one-time secret.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public sealed record StudioCreatedWebhook(string? Path, string? Method, string Secret, string InvokePath);
