using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Application;

public sealed class RecordedOutputReplayService(
	IRecordedOutputQueryService recordedOutputs,
	IServiceTaskRegistry serviceTaskRegistry) : IRecordedOutputReplayService
{
	internal const string ReplayImplementationPrefix = "__replay__:";

	public async Task<BpmnModel> RewriteForReplayAsync(
		string tenantId,
		string processDefinitionKey,
		BpmnModel model,
		CancellationToken cancellationToken = default)
	{
		var tasks = model.Tasks is null ? null : model.Tasks.ToList();
		if (tasks is null)
		{
			return model;
		}

		for (var i = 0; i < tasks.Count; i++)
		{
			var task = tasks[i];
			if (!string.Equals(task.Type, "serviceTask", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var recorded = await recordedOutputs.GetLastRecordedOutputAsync(
				tenantId, processDefinitionKey, task.Id, cancellationToken);

			if (recorded is not { Count: > 0 })
			{
				continue;
			}

			var replayKey = ReplayImplementationPrefix + task.Id;
			tasks[i] = task with
			{
				Implementation = replayKey,
				Attributes = task.Attributes is null
					? new Dictionary<string, string>(StringComparer.Ordinal)
					: new Dictionary<string, string>(task.Attributes, StringComparer.Ordinal)
			};
			serviceTaskRegistry.Register(replayKey, new RecordedOutputServiceTaskHandler(recorded));
		}

		return model with { Tasks = tasks };
	}
}
