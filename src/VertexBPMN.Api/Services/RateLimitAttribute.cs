using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Rate limiting action filter attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class RateLimitAttribute : ActionFilterAttribute
{
	public string Policy { get; }
	public RateLimitAttribute(string policy = "default")
	{
		Policy = policy;
	}

	public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		var rateLimitingService = context.HttpContext.RequestServices.GetRequiredService<IRateLimitingService>();
		// Get identifier (IP address or user ID)
		var identifier = GetIdentifier(context.HttpContext);
		if (!rateLimitingService.IsAllowed(identifier, Policy))
		{
			var rateLimitInfo = rateLimitingService.GetRateLimitInfo(identifier, Policy);
			context.HttpContext.Response.StatusCode = 429; // Too Many Requests
			context.HttpContext.Response.Headers["X-RateLimit-Limit"] = rateLimitInfo.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
			context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimitInfo.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
			context.HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimitInfo.ResetTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
			context.HttpContext.Response.Headers["Retry-After"] = ((int)rateLimitInfo.WindowDuration.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
			await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", context.HttpContext.RequestAborted);
			return;
		}

		// Add rate limit headers to successful responses
		var info = rateLimitingService.GetRateLimitInfo(identifier, Policy);
		context.HttpContext.Response.Headers["X-RateLimit-Limit"] = info.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
		context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = info.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
		context.HttpContext.Response.Headers["X-RateLimit-Reset"] = info.ResetTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
		await next();
	}

	private static string GetIdentifier(HttpContext context)
	{
		// Try to get user ID first
		var userId = context.User?.Identity?.Name;
		if (!string.IsNullOrEmpty(userId))
		{
			return $"user:{userId}";
		}

		// Fall back to IP address
		var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		// Trusted forwarding is handled by Program's configured ForwardedHeaders middleware.
		// Reading raw headers here would allow callers to bypass the per-client budget.
		return $"ip:{ipAddress}";
	}
}
