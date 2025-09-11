namespace VertexBPMN.Core.Bpmn;

public record BpmnSubprocess(string Id, bool IsMultiInstance, bool IsEventSubprocess = false, bool IsTransaction = false, bool IsSequential = false, int? LoopCardinality = null);