using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace VertexBPMN.Domain.Model;

internal static class Util
{
    public static string? Attr(this XElement el, XName name) => (string?)el.Attribute(name) ?? "";
    public static bool? AttrBool(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? bool.Parse(a.Value) : false;
    public static int? AttrInt(this XElement el, XName name) => el.Attribute(name) is XAttribute a ? int.Parse(a.Value) : 0;

    public static XName B(this string local) => Ns.BPMN + local;
    public static XName BPMNDI(this string local) => Ns.BPMNDI + local;
    public static XName DI(this string local) => Ns.DI + local;
    public static XName DC(this string local) => Ns.DC + local;
    public static XName D(this string local) => Ns.DMN + local;
    public static XName DMNDI(this string local) => Ns.DMNDI + local;
    public static XName N(this string local) => Ns.DMN + local;

    public static List<T> AddRange<T>(this List<T> source, IEnumerable<T> items)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (items == null) throw new ArgumentNullException(nameof(items));
        var result = new List<T>(source.Count);
        for (int i = 0; i < source.Count; i++) result.Add(source[i]);
        foreach (var x in items) result.Add(x);
        return result;
    }

    public static List<T> Add<T>(this List<T> source, T item)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var result = new List<T>(source.Count + 1);
        for (int i = 0; i < source.Count; i++) result.Add(source[i]);
        result.Add(item);
        return result;
    }

    public static string? A(this XElement el, XName n) => (string?)el.Attribute(n);

    public static double? Ad(this XElement el, XName n) =>
        el.Attribute(n) is XAttribute a ? double.Parse(a.Value) : null;

}
