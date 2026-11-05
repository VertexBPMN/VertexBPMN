namespace VertexBPMN.Studio.Services;

internal interface IFieldMappingService
{
	Task<StudioFieldMappingPreviewResult> PreviewAsync(string? sourceJson, IReadOnlyList<StudioFieldMappingItem> mappings, CancellationToken cancellationToken = default);
}
