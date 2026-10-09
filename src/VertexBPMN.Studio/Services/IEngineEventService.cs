namespace VertexBPMN.Studio.Services;

internal interface IEngineEventService
{
    event EventHandler<EngineMessageEventArgs> OnEventEmitted;
}
