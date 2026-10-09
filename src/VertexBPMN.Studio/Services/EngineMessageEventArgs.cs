namespace VertexBPMN.Studio.Services;

internal sealed class EngineMessageEventArgs(string value) : EventArgs
{
	public string Message { get; } = value;
}
