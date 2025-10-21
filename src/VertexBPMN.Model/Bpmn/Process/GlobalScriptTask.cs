namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Global script task, as per Figure 10.44.
/// </summary>
public record GlobalScriptTask(
    string ScriptLanguage,
    string Script
) : GlobalTask;