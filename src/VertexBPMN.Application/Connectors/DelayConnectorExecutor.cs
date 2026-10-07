using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public sealed class DelayConnectorExecutor : IConnectorExecutor
{
	public string Type => "delay";
	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		var milliseconds = context.Attributes.TryGetValue("vertex:connector.delayMs", out var value) && int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 0, 86_400_000) : 0;
		await Task.Delay(milliseconds, cancellationToken);
		return new ConnectorExecutionResult(true, null, new Dictionary<string, object>(StringComparer.Ordinal) { ["delayedMs"] = milliseconds });
	}
}
