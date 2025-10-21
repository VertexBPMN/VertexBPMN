namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Transaction, as per Figure 10.29.
/// </summary>
public record Transaction(
    string Method
) : SubProcess;