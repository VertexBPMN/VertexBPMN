using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type LoadPluginRequest retained for exposed HTTP action signatures and controller activation (LoadPluginRequest).")]
public sealed class LoadPluginRequest
{
	public string PluginPath { get; set; } = string.Empty;
}
