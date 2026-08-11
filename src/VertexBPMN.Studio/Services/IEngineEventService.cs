namespace VertexBPMN.Studio.Services;

public interface IEngineEventService
{
    event Action<string> OnEventEmitted;
}

public sealed class EngineEventService : IEngineEventService
{
    private readonly IBpmnEngineService _engineService;

    public EngineEventService(IBpmnEngineService engineService)
    {
        _engineService = engineService;
        _engineService.OnEventEmitted += ForwardEvent;
    }

    public event Action<string>? OnEventEmitted;

    private void ForwardEvent(string message) => OnEventEmitted?.Invoke(message);
}
