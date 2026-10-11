namespace VertexBPMN.Studio.Services;

internal sealed class EngineEventService : IEngineEventService, IDisposable
{
	private readonly IBpmnEngineService _engineService;
	public EngineEventService(IBpmnEngineService engineService)
	{
		_engineService = engineService;
		_engineService.OnEventEmitted += ForwardEvent;
	}

	public event EventHandler<EngineMessageEventArgs>? OnEventEmitted;
	private void ForwardEvent(object? sender, EngineMessageEventArgs message) => OnEventEmitted?.Invoke(this, message);
	public void Dispose()
	{
		_engineService.OnEventEmitted -= ForwardEvent;
		OnEventEmitted = null;
	}
}
