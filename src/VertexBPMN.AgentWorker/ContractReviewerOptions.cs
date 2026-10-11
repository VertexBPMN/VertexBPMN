using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;
internal sealed class ContractReviewerOptions
{
	public const string SectionName = "ContractReviewer";
	public bool Enabled { get; set; }
	public string Profile { get; set; } = "contract-reviewer.v1";
	public string DataClassification { get; set; } = "local-sensitive";
	public string Adapter { get; set; } = "ollama";
	public string Endpoint { get; set; } = "http://localhost:11434/";
	public string Model { get; set; } = string.Empty;
	public int MaximumDocumentBytes { get; set; } = 65_536;
	public int MaximumSectionCharacters { get; set; } = 4_000;
	public int MaximumSections { get; set; } = 64;
	public int MaximumSearchHits { get; set; } = 8;
	public int MaximumToolCalls { get; set; } = 8;
	public int MaximumRuntimeCalls { get; set; } = 10;
	public int MaximumOutputTokensPerCall { get; set; } = 768;
	public int TotalTokenBudget { get; set; } = 12_000;
	public int MaximumResponseBytes { get; set; } = 32_768;
	public int MaximumRuntimeSeconds { get; set; } = 120;
}
