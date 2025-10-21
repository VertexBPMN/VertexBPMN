using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Cmmn.Xml;

internal static class Util
{
    public static string? Attr(this XElement el, XName name) => (string?)el.Attribute(name);
    public static bool? AttrBool(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? bool.Parse(a.Value) : null;
    public static double? AttrDouble(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? double.Parse(a.Value) : null; 

}