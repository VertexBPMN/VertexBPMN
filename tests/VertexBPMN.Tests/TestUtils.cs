
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace VertexBPMN.Tests;

internal static class TestUtils
{
	public static async Task<HttpResponseMessage> WaitForEndpointAsync(HttpClient client, string relativeUrl, int maxRetries = 10, int delayMs = 500)
	{
		var policy = Policy
			.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
			.WaitAndRetryAsync(maxRetries, _ => TimeSpan.FromMilliseconds(delayMs));

		return await policy.ExecuteAsync(() => client.GetAsync(relativeUrl));
	}
}

