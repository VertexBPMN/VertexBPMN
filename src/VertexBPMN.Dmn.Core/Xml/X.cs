using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Dmn.Xml;

internal static class X
{
    public static string? A(this XElement el, XName n) => (string?) el.Attribute(n);

    public static double? Ad(this XElement el, XName n) =>
        el.Attribute(n) is XAttribute a ? double.Parse(a.Value) : null;
}