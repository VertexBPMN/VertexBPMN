using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Bpmn.Xml;

internal static class Util
{
    public static string? Attr(this XElement el, XName name) => (string?)el.Attribute(name);
    public static bool? AttrBool(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? bool.Parse(a.Value) : null;
    public static int? AttrInt(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? int.Parse(a.Value) : null;
    public static XName B(this string local) => XNamespace.Get(Ns.BPMN) + local;
    public static XName BPMNDI(this string local) => XNamespace.Get(Ns.BPMNDI) + local;
    public static XName DI(this string local) => XNamespace.Get(Ns.DI) + local;
    public static XName DC(this string local) => XNamespace.Get(Ns.DC) + local;

    public static IReadOnlyList<T> AddRange<T>(this IReadOnlyList<T> source, IEnumerable<T> items)
    {
        if (source == null) throw new ArgumentNullException(nameof(source)); if (items == null) throw new ArgumentNullException(nameof(items));

        var result = new List<T>(source.Count);
        for (int i = 0; i < source.Count; i++)
            result.Add(source[i]);

        foreach (var x in items)
            result.Add(x);

        return result;
    }
    public static IReadOnlyList<T> Add<T>(this IReadOnlyList<T> source, T item)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var result = new List<T>(source.Count + 1);
        for (int i = 0; i < source.Count; i++)
            result.Add(source[i]);

        result.Add(item);
        return result;
    }
}