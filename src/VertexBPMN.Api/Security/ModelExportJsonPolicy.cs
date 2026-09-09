using System.Text.Json.Serialization.Metadata;
using VertexBPMN.Application;

namespace VertexBPMN.Api.Security;

public static class ModelExportJsonPolicy
{
    public static void Apply(JsonTypeInfo info)
    {
        foreach (var property in info.Properties)
        {
            if (property.PropertyType != typeof(string) || property.Get is not { } getter) continue;
            if (property.Name.Equals("bpmnXml", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("bpmn20Xml", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("dmnXml", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("cmmnXml", StringComparison.OrdinalIgnoreCase))
                property.Get = instance => getter(instance) is string xml ? ModelExportRedaction.Redact(xml) : null;
        }
    }
}
