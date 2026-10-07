using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Application;

/// <summary>
/// CLI test-runner helper (Plan §3.6): rewrites a parsed BpmnModel so that every
/// service task with a prior recorded <c>TASK_IO_SNAPSHOT</c> output is dispatched
/// to a <see cref="RecordedOutputServiceTaskHandler"/> instead of a live connector.
/// This is a purely CLI-local operation — it mutates only the in-memory model and
/// registers replay handlers on the in-process <see cref="IServiceTaskRegistry"/>.
/// It never touches the production <c>JobExecutorService</c> path.
/// </summary>
public interface IRecordedOutputReplayService
{
	Task<BpmnModel> RewriteForReplayAsync(
		string tenantId,
		string processDefinitionKey,
		BpmnModel model,
		CancellationToken cancellationToken = default);
}
