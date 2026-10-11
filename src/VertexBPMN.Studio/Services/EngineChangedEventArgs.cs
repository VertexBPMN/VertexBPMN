namespace VertexBPMN.Studio.Services;

internal sealed class EngineChangedEventArgs(string value) : EventArgs
{
	public string EngineId { get; } = value;
}
