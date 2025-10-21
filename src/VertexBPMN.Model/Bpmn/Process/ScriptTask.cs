namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Script task, as per Figure 10.10.
/// </summary>
public record ScriptTask(
    string ScriptFormat,
    string Script
) : Task;