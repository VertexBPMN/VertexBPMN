using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;
internal sealed class ExternalTaskWorkerOptions
{
	public const string SectionName = "ExternalTaskWorker";
	public bool Enabled { get; set; }
	public string BaseAddress { get; set; } = string.Empty;
	public string TokenEndpoint { get; set; } = string.Empty;
	public string ClientId { get; set; } = string.Empty;
	public string ClientSecret { get; set; } = string.Empty;
	public string Scope { get; set; } = string.Empty;
	public string[] Topics { get; set; } = [];
	public int MaxConcurrency { get; set; } = 1;
	public int MaxTasksPerClaim { get; set; } = 1;
	public int LeaseSeconds { get; set; } = 60;
	public int HeartbeatSeconds { get; set; } = 20;
	public int PollMilliseconds { get; set; } = 1000;
	public int MaximumBackoffSeconds { get; set; } = 60;
}
