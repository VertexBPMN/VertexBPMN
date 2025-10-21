namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Task, as per Figure 10.10.
/// </summary>
public record Task() : Activity
{
    public Dictionary<string, string>? Attributes { get; set; } = new Dictionary<string, string>();
    public string Implementation { get; internal set; }
}