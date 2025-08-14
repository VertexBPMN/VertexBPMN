namespace VertexBPMN.Core.Bpmn;

public record BpmnEvent(string Id, string Type, string? AttachedToRef = null, bool IsCompensation = false, bool CancelActivity = true, string? EventDefinitionType = null);