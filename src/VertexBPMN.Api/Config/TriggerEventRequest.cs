using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Config;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type TriggerEventRequest retained for exposed HTTP action signatures and controller activation (TriggerEventRequest).")]
public record TriggerEventRequest(string CaseId, string EventId, Dictionary<string, object> EventData);
