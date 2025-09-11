using System.Collections.Generic;

namespace VertexBPMN.Api.Dto;

public class VariableValueDto
{
    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }
    public IDictionary<string, object>? ValueInfo { get; set; }
}
