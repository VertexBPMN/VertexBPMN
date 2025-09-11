using System;

namespace VertexBPMN.Domain.Debugging;

public class VisualToken
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}