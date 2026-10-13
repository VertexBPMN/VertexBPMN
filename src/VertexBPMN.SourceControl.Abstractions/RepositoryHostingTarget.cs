namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Secret-free repository identity captured by a fixed server-side hosting policy.</summary>
public sealed record RepositoryHostingTarget(string ProviderId, string RepositoryId,
	Uri ApiRepositoryUri, Uri WebRepositoryUri)
{
	[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
	public string? ApiVersion { get; init; }
}
