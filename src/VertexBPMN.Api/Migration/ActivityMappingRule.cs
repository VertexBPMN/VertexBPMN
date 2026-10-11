namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type ActivityMappingRule retained for exposed HTTP action signatures and controller activation (ActivityMappingRule).")]
public class ActivityMappingRule
{
    public string FromActivityId { get; set; } = string.Empty;
    public string ToActivityId { get; set; } = string.Empty;
    public string MappingType { get; set; } = string.Empty; // Direct, Transform, Custom
}
