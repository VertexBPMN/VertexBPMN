using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed class ExternalTaskLeaseException(string code) : InvalidOperationException(code)
{
	public string Code { get; } = code;
}
