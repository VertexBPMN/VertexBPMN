using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/decision")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class DecisionController : ControllerBase
{
	private readonly IDecisionService _decisionService;
	public DecisionController(IDecisionService decisionService)
	{
		_decisionService = decisionService;
	}

	[HttpPost("deploy")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> Deploy([FromBody] DeployRequest request)
	{
		var tenantId = ResolveTenantId(request.TenantId);
		if (tenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		try
		{
			await _decisionService.DeployAsync(request.DecisionKey, request.Name, request.DmnXml, tenantId);
			return Ok();
		}
		catch (Exception exception)when (exception is ArgumentException or InvalidOperationException or System.Xml.XmlException)
		{
			return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "DMN document is outside the supported decision-table subset");
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type DeployRequest retained for exposed HTTP action signatures and controller activation (DecisionController).")]
	public record DeployRequest(string DecisionKey, string Name, string DmnXml, string? TenantId = null);
	[HttpGet("by-key")]
	public async Task<ActionResult<DecisionDefinition>> GetDecisionByKey([FromQuery] string decisionKey, [FromQuery] string? tenantId = null)
	{
		var effectiveTenantId = ResolveTenantId(tenantId);
		if (effectiveTenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var def = await _decisionService.GetDecisionByKeyAsync(decisionKey, effectiveTenantId);
		return def switch
		{
			null => NotFound(),
			_ => new ActionResult<DecisionDefinition>(def)
		};
	}

	/// <summary>
	/// Evaluates a DMN decision by key with input variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Example request:
	///
	///     POST /api/decision/evaluate
	///     {
	///         "DecisionKey": "my-decision",
	///         "Inputs": { "input1": "value" }
	///     }
	/// </para>
	/// </remarks>
	/// <param name = "request">Evaluation request</param>
	/// <returns>The DMN decision result</returns>
	[HttpPost("evaluate")]
	[ProducesResponseType(typeof(DecisionResult), 200)]
	public async Task<ActionResult<DecisionResult>> Evaluate([FromBody] EvaluateRequest request)
	{
		var tenantId = ResolveTenantId(request.TenantId);
		if (tenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var result = await _decisionService.EvaluateDecisionByKeyAsync(request.DecisionKey, request.Inputs, tenantId);
		return Ok(result);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type EvaluateRequest retained for exposed HTTP action signatures and controller activation (DecisionController).")]
	public record EvaluateRequest(string DecisionKey, IDictionary<string, object> Inputs, string? TenantId = null);
	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}
}
