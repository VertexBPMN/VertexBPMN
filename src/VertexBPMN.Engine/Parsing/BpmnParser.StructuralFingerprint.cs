using System.Text;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private static void AppendFingerprintField(StringBuilder sb, string prefix, string? value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			sb.Append(prefix).Append(value);
		}
	}

	private static void AppendHashedFingerprintField(StringBuilder sb, string prefix, string? value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			sb.Append($"{prefix}{StringComparer.Ordinal.GetHashCode(value):X}");
		}
	}

	private static void AppendEventsStructure(StringBuilder sb, BpmnModel model)
	{
		// Events (sorted by id for deterministic output)
		sb.Append("EVENTS:");
		foreach (var evt in (model.Events ?? []).OrderBy(e => e.Id, StringComparer.Ordinal))
		{
			sb.Append($"{evt.Id}:{evt.Type}");
			if (!string.IsNullOrEmpty(evt.Name))
			{
				sb.Append($"#{evt.Name}");
			}

			// Event definitions (sorted for consistency)
			if ((evt.Definitions ?? []).Count > 0)
			{
				sb.Append('[');
				foreach (var def in (evt.Definitions ?? []).OrderBy(d => d.GetType().Name, StringComparer.Ordinal))
				{
					sb.Append($"{def.GetType().Name}:");
					AppendEventDefinitionStructure(sb, def);
					sb.Append(',');
				}
				sb.Append(']');
			}
			sb.Append('|');
		}
	}

	private static void AppendTasksStructure(StringBuilder sb, BpmnModel model)
	{
		// Tasks (sorted by id for deterministic output)
		sb.Append("TASKS:");
		foreach (var task in (model.Tasks ?? []).OrderBy(t => t.Id, StringComparer.Ordinal))
		{
			sb.Append($"{task.Id}:{task.Type}");
			if (!string.IsNullOrEmpty(task.Name))
			{
				sb.Append($"#{task.Name}");
			}

			sb.Append('|');
		}
	}

	private static void AppendGatewaysStructure(StringBuilder sb, BpmnModel model)
	{
		// Gateways (sorted by id for deterministic output)
		sb.Append("GATEWAYS:");
		foreach (var gateway in (model.Gateways ?? []).OrderBy(g => g.Id, StringComparer.Ordinal))
		{
			sb.Append($"{gateway.Id}:{gateway.Type}");
			if (!string.IsNullOrEmpty(gateway.DefaultFlowId))
			{
				sb.Append($"@{gateway.DefaultFlowId}");
			}

			sb.Append('|');
		}
	}

	private static void AppendSubprocessesStructure(StringBuilder sb, BpmnModel model)
	{
		// Subprocesses (sorted by id for deterministic output)
		sb.Append("SUBPROCESSES:");
		foreach (var subprocess in (model.Subprocesses ?? []).OrderBy(s => s.Id, StringComparer.Ordinal))
		{
			sb.Append($"{subprocess.Id}:SP");
			if (subprocess.IsEventSubprocess)
			{
				sb.Append(":EVT");
			}

			if (subprocess.IsTransaction)
			{
				sb.Append(":TX");
			}

			sb.Append('|');
		}
	}

	private static void AppendFlowsStructure(StringBuilder sb, BpmnModel model)
	{
		// Sequence flows (sorted by id for deterministic output)
		sb.Append("FLOWS:");
		foreach (var flow in (model.SequenceFlows ?? []).OrderBy(f => f.Id, StringComparer.Ordinal))
		{
			sb.Append($"{flow.Id}:{flow.SourceRef}->{flow.TargetRef}");
			if (flow.IsDefault)
			{
				sb.Append(":DEF");
			}

			if (!string.IsNullOrEmpty(flow.ConditionExpression))
			{
				sb.Append($":COND#{StringComparer.Ordinal.GetHashCode(flow.ConditionExpression):X}"); // Hash condition to avoid huge strings
			}

			if (flow.Priority.HasValue)
			{
				sb.Append($":PRI{flow.Priority.Value}");
			}

			sb.Append('|');
		}
	}

	private static void AppendGlobalsStructure(StringBuilder sb, BpmnModel model)
	{
		AppendGlobalMessagesStructure(sb, model);
		AppendGlobalSignalsStructure(sb, model);
		AppendGlobalErrorsStructure(sb, model);
		AppendGlobalEscalationsStructure(sb, model);
	}
	private static void AppendGlobalMessagesStructure(StringBuilder sb, BpmnModel model)
	{
		if ((model.Messages ?? []).Count > 0)
		{
			sb.Append("MESSAGES:");
			foreach (var msg in (model.Messages ?? []).OrderBy(m => m.Id, StringComparer.Ordinal))
			{
				sb.Append($"{msg.Id}");
				if (!string.IsNullOrEmpty(msg.Name))
				{
					sb.Append($"#{msg.Name}");
				}

				sb.Append('|');
			}
		}


	}

	private static void AppendGlobalSignalsStructure(StringBuilder sb, BpmnModel model)
	{
		if ((model.Signals ?? []).Count > 0)
		{
			sb.Append("SIGNALS:");
			foreach (var sig in (model.Signals ?? []).OrderBy(s => s.Id, StringComparer.Ordinal))
			{
				sb.Append($"{sig.Id}");
				if (!string.IsNullOrEmpty(sig.Name))
				{
					sb.Append($"#{sig.Name}");
				}

				sb.Append('|');
			}
		}


	}

	private static void AppendGlobalErrorsStructure(StringBuilder sb, BpmnModel model)
	{
		if ((model.Errors ?? []).Count > 0)
		{
			sb.Append("ERRORS:");
			foreach (var err in (model.Errors ?? []).OrderBy(e => e.Id, StringComparer.Ordinal))
			{
				sb.Append($"{err.Id}");
				if (!string.IsNullOrEmpty(err.Name))
				{
					sb.Append($"#{err.Name}");
				}

				if (!string.IsNullOrEmpty(err.ErrorCode))
				{
					sb.Append($":{err.ErrorCode}");
				}

				sb.Append('|');
			}
		}


	}

	private static void AppendGlobalEscalationsStructure(StringBuilder sb, BpmnModel model)
	{
		if ((model.Escalations ?? []).Count > 0)
		{
			sb.Append("ESCALATIONS:");
			foreach (var esc in (model.Escalations ?? []).OrderBy(e => e.Id, StringComparer.Ordinal))
			{
				sb.Append($"{esc.Id}");
				if (!string.IsNullOrEmpty(esc.Name))
				{
					sb.Append($"#{esc.Name}");
				}

				if (!string.IsNullOrEmpty(esc.EscalationCode))
				{
					sb.Append($":{esc.EscalationCode}");
				}

				sb.Append('|');
			}
		}
	}



}
