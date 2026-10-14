using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

internal sealed class HttpFieldMappingService(IHttpClientFactory httpClientFactory) : IFieldMappingService
{
	public async Task<StudioFieldMappingPreviewResult> PreviewAsync(string? sourceJson, IReadOnlyList<StudioFieldMappingItem> mappings, CancellationToken cancellationToken = default)
	{
		var client = httpClientFactory.CreateClient("VertexBPMN.Api");
		using var response = await client.PostAsJsonAsync("/api/mapping/preview", new { sourceJson, mappings }, cancellationToken);
		await ApiResponseErrors.EnsureSuccessAsync(response, cancellationToken);
		return await response.Content.ReadFromJsonAsync<StudioFieldMappingPreviewResult>(cancellationToken)
			?? throw new InvalidOperationException("The API returned no mapping preview result.");
	}
}
