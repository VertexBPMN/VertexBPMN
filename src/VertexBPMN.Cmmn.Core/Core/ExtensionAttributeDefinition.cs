using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed class ExtensionAttributeDefinition : CmmnElement
{
    public Qname AttributeName { get; set; }
    public UriString TypeUri { get; set; }
    public ExtensionAttributeDefinition(Qname attributeName, UriString typeUri)
    {
        AttributeName = attributeName;
        TypeUri = typeUri;
    }
}