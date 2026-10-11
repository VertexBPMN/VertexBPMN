using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type ExecuteMethodRequest retained for exposed HTTP action signatures and controller activation (ExecuteMethodRequest).")]
public sealed class ExecuteMethodRequest
{
	public object[] Parameters { get; set; } = Array.Empty<object>();
}
