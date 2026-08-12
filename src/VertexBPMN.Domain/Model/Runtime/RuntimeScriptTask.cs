namespace VertexBPMN.Domain.Model.Runtime;

public sealed record RuntimeScriptTask(
    string ScriptFormat,
    string ScriptBody,
    string? ResultVariable
);