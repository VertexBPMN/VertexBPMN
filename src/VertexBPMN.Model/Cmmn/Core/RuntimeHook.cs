using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Runtime hook for extensions (e.g., lifecycle events; custom extension).
/// </summary>
public record RuntimeHook(
    string EventType, // e.g., "OnStateChange".
    Expression Callback // Expression to execute on event.
);