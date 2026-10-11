using System.Net;
using System.Text;
using System.Xml;

namespace VertexBPMN.Engine.Security;

internal sealed class CountingXmlResolver : XmlResolver
{
	public int RequestCount { get; private set; }

	public override ICredentials? Credentials
	{
		// The diagnostic resolver never authenticates or performs network requests.
		set => _ = value;
	}

	public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
	{
		// Zähle jeden Versuch, eine externe DTD/Entität zu laden
		RequestCount++;

		// Liefere eine minimale externe DTD mit einer Entität "ext"
		const string dtd = "<!ENTITY ext 'EXTERNAL_ENTITY_RESOLVED'>";
		return new MemoryStream(Encoding.UTF8.GetBytes(dtd));
	}

	public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
	{
		return baseUri == null ? new Uri(relativeUri!, UriKind.Absolute)
			: new Uri(baseUri, relativeUri);
	}
}
