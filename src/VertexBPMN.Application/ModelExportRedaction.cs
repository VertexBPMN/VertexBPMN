using System.Xml;
using System.Xml.Linq;

namespace VertexBPMN.Application;

/// <summary>Redacts structured inline credentials only at export boundaries, never in stored executable models.</summary>
public static class ModelExportRedaction
{
    public const string Marker = "[VERTEX-REDACTED]";
    private static readonly HashSet<string> SecretNames = new(StringComparer.OrdinalIgnoreCase)
    { "password", "passwd", "secret", "clientsecret", "apikey", "accesstoken", "refreshtoken", "authorization", "connectionstring" };

    private static bool Sensitive(string name) => SecretNames.Contains(name.Replace("_", "").Replace("-", ""));

    public static string Redact(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return xml;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10_000_000 });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var changed = false;
            foreach (var element in document.Descendants().ToArray())
            {
                var namedSecret = Sensitive(element.Name.LocalName)
                    || element.Attributes().Any(a => a.Name.LocalName is "name" or "key" && Sensitive(a.Value));
                if (namedSecret && !element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                {
                    element.Value = Marker;
                    changed = true;
                }
                foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration).ToArray())
                {
                    if (Sensitive(attribute.Name.LocalName)
                        || namedSecret && attribute.Name.LocalName == "value"
                        || ContainsUrlSecret(attribute.Value))
                    {
                        attribute.Value = Marker;
                        changed = true;
                    }
                }
            }
            if (!changed) return xml; // Preserve exact roundtrip for models without inline credentials.
            document.Root?.SetAttributeValue(XName.Get("redacted", "https://vertexbpmn.io/schema/bpmn/1.0"), "true");
            foreach (var process in document.Descendants().Where(e => e.Name.LocalName == "process"))
                process.SetAttributeValue("isExecutable", "false");
            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch (XmlException)
        {
            return $"<redacted>{Marker}</redacted>";
        }
    }

    private static bool ContainsUrlSecret(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
        return !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.TrimStart('?').Split('&')
            .Any(part => Sensitive(Uri.UnescapeDataString(part.Split('=')[0])));
    }
}
