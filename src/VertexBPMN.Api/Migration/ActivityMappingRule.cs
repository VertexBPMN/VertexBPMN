namespace VertexBPMN.Api.Migration;

public class ActivityMappingRule
{
    public string FromActivityId { get; set; } = string.Empty;
    public string ToActivityId { get; set; } = string.Empty;
    public string MappingType { get; set; } = string.Empty; // Direct, Transform, Custom
}