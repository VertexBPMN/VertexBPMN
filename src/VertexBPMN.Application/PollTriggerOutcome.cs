using System.Text.Json;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>Result of a single polling cycle; carries the new cursor state and, on success, the started instance.</summary>
public sealed record PollTriggerOutcome(
	PollTriggerStatus Status,
	Guid? InstanceId,
	Dictionary<string, object> NewCursorState,
	string? ErrorCode = null)
{
	public static PollTriggerOutcome StartedInstance(Guid instanceId, Dictionary<string, object> cursor)
		=> new(PollTriggerStatus.Started, instanceId, cursor);

	public static PollTriggerOutcome Idle(Dictionary<string, object> cursor)
		=> new(PollTriggerStatus.Idle, null, cursor);

	public static PollTriggerOutcome Failed(string? code)
		=> new(PollTriggerStatus.Failed, null, new Dictionary<string, object>(StringComparer.Ordinal), code);
}
