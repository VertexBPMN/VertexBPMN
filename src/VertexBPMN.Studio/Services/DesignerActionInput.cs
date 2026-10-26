namespace VertexBPMN.Studio.Services;

/// <summary>One named input parameter of an action (e.g. a task variable or read constraint).</summary>
internal sealed record DesignerActionInput(string Name, string Value);
