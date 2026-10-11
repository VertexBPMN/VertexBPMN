using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Api.Security;

/// <summary>
/// Security Headers Middleware
/// </summary>
internal class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;
	public SecurityHeadersMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// Security headers
		context.Response.Headers["X-Content-Type-Options"] = "nosniff";
		context.Response.Headers["X-Frame-Options"] = "DENY";
		context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
		context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
		context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
		context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
		await _next(context);
	}
}
