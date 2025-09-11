using System;
using System.Collections.Generic;

namespace VertexBPMN.Api.Dto;

public class ProcessInstanceDto
{
    public Guid Id { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public IDictionary<string, object>? Variables { get; set; }
}
