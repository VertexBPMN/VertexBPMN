using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace VertexBPMN.Domain.Model.Bpmn;

// 1) Vendor handler interface
public interface IExtensionHandler
{
    string Namespace { get; }
    string Prefix { get; }
    // Serialize the entire variable map into one vendor-specific root element
    XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null);
    // Parse one vendor-specific root element into a variable dictionary
    IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null);
    // Identify if this handler owns the given element
    bool Matches(XmlElement el) => el.NamespaceURI == Namespace;
}

// 2) Built-in handlers
public class CamundaExtensionHandler : IExtensionHandler
{
    public string Namespace => "http://camunda.org/schema/1.0/bpmn";
    public string Prefix => "camunda";
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        var root = doc.CreateElement(Prefix, "properties", Namespace);
        foreach (var kv in vars)
        {
            var prop = doc.CreateElement(Prefix, "property", Namespace);
            var nameAttr = doc.CreateAttribute("name"); nameAttr.Value = kv.Key; prop.Attributes.Append(nameAttr);
            var valueAttr = doc.CreateAttribute("value"); valueAttr.Value = kv.Value?.ToString() ?? string.Empty; prop.Attributes.Append(valueAttr);
            root.AppendChild(prop);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement prop in root.ChildNodes.OfType<XmlElement>())
        {
            if (prop.LocalName != "property" || prop.NamespaceURI != Namespace) continue;
            var name = prop.GetAttribute("name");
            var val = prop.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = VariableParsing.HeuristicParse(val, typeMap != null && typeMap.TryGetValue(name, out var t) ? t : null);
        }
        return result;
    }
}

public class FlowableExtensionHandler : IExtensionHandler
{
    public string Namespace => "http://flowable.org/bpmn";
    public string Prefix => "flowable";
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        var root = doc.CreateElement(Prefix, "properties", Namespace);
        foreach (var kv in vars)
        {
            var prop = doc.CreateElement(Prefix, "property", Namespace);
            prop.SetAttribute("name", kv.Key);
            prop.SetAttribute("value", kv.Value?.ToString() ?? string.Empty);
            root.AppendChild(prop);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement prop in root.ChildNodes.OfType<XmlElement>())
        {
            if (prop.LocalName != "property" || prop.NamespaceURI != Namespace) continue;
            var name = prop.GetAttribute("name");
            var val = prop.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = VariableParsing.HeuristicParse(val, typeMap != null && typeMap.TryGetValue(name, out var t) ? t : null);
        }
        return result;
    }
}

public class ActivitiExtensionHandler : IExtensionHandler
{
    public string Namespace => "http://activiti.org/bpmn";
    public string Prefix => "activiti";
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        var root = doc.CreateElement(Prefix, "properties", Namespace);
        foreach (var kv in vars)
        {
            var prop = doc.CreateElement(Prefix, "property", Namespace);
            prop.SetAttribute("name", kv.Key);
            prop.SetAttribute("value", kv.Value?.ToString() ?? string.Empty);
            root.AppendChild(prop);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement prop in root.ChildNodes.OfType<XmlElement>())
        {
            if (prop.LocalName != "property" || prop.NamespaceURI != Namespace) continue;
            var name = prop.GetAttribute("name");
            var val = prop.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = VariableParsing.HeuristicParse(val, typeMap != null && typeMap.TryGetValue(name, out var t) ? t : null);
        }
        return result;
    }
}

public class ZeebeIoExtensionHandler : IExtensionHandler
{
    public string Namespace => "http://zeebe.io/schema/zeebe/1.0";
    public string Prefix => "zeebe";
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        // Best-effort: constants as inputs -> process vars
        var root = doc.CreateElement(Prefix, "ioMapping", Namespace);
        foreach (var kv in vars)
        {
            var inp = doc.CreateElement(Prefix, "input", Namespace);
            inp.SetAttribute("source", "=" + (kv.Value?.ToString() ?? string.Empty));
            inp.SetAttribute("target", kv.Key);
            root.AppendChild(inp);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement el in root.ChildNodes.OfType<XmlElement>())
        {
            if (el.LocalName != "input" || el.NamespaceURI != Namespace) continue;
            var name = el.GetAttribute("target");
            var source = el.GetAttribute("source");
            var val = source?.TrimStart('=');
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = VariableParsing.HeuristicParse(val, typeMap != null && typeMap.TryGetValue(name, out var t) ? t : null);
        }
        return result;
    }
}

public class VertexBpmnExtensionHandler : IExtensionHandler
{
    public string Namespace => "http://vertexbpmn.com/bpmn/ext";
    public string Prefix => "vbpmn";
    private const string VarsEl = "processVariables";
    private const string VarEl = "var";
    private const string AttrName = "name";
    private const string AttrType = "type";
    private const string AttrAqn = "aqn";
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        var root = doc.CreateElement(Prefix, VarsEl, Namespace);
        foreach (var kv in vars)
        {
            var valType = typeMap != null && typeMap.TryGetValue(kv.Key, out var tHint) ? tHint : kv.Value?.GetType() ?? typeof(string);
            var el = doc.CreateElement(Prefix, VarEl, Namespace);
            el.SetAttribute(AttrName, kv.Key);
            el.SetAttribute(AttrType, valType.FullName ?? "System.String");
            el.SetAttribute(AttrAqn, valType.AssemblyQualifiedName ?? "");
            el.InnerText = kv.Value?.ToString() ?? string.Empty;
            root.AppendChild(el);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement el in root.ChildNodes.OfType<XmlElement>())
        {
            if (el.LocalName != VarEl || el.NamespaceURI != Namespace) continue;
            var name = el.GetAttribute(AttrName);
            var typeName = el.GetAttribute(AttrAqn);
            if (string.IsNullOrWhiteSpace(typeName)) typeName = el.GetAttribute(AttrType);
            var text = el.InnerText;
            if (string.IsNullOrWhiteSpace(name)) continue;
            var targetType = (typeMap != null && typeMap.TryGetValue(name, out var mapType)) ? mapType : Type.GetType(typeName, false);
            result[name] = VariableParsing.ConvertTo(text, targetType) ?? VariableParsing.HeuristicParse(text, targetType);
        }
        return result;
    }
}

// Optional generic handler for unknown vendors: emit <vendor:properties><vendor:property name="" value=""/></vendor:properties>
public class GenericExtensionHandler : IExtensionHandler
{
    public string Namespace { get; }
    public string Prefix { get; }
    public GenericExtensionHandler(string ns, string prefix) { Namespace = ns; Prefix = prefix; }
    public XmlElement Serialize(XmlDocument doc, IDictionary<string, object> vars, IDictionary<string, Type>? typeMap = null)
    {
        var root = doc.CreateElement(Prefix, "properties", Namespace);
        foreach (var kv in vars)
        {
            var prop = doc.CreateElement(Prefix, "property", Namespace);
            prop.SetAttribute("name", kv.Key);
            prop.SetAttribute("value", kv.Value?.ToString() ?? string.Empty);
            root.AppendChild(prop);
        }
        return root;
    }
    public IDictionary<string, object> Deserialize(XmlElement root, IDictionary<string, Type>? typeMap = null)
    {
        var result = new Dictionary<string, object>();
        foreach (XmlElement prop in root.ChildNodes.OfType<XmlElement>())
        {
            if (prop.LocalName != "property") continue;
            var name = prop.GetAttribute("name");
            var val = prop.GetAttribute("value");
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = VariableParsing.HeuristicParse(val, typeMap != null && typeMap.TryGetValue(name, out var t) ? t : null);
        }
        return result;
    }
}

// 3) Helper for parsing
internal static class VariableParsing
{
    public static object? ConvertTo(string input, Type? t)
    {
        try
        {
            if (t == null) return null;
            if (t == typeof(string)) return input;
            if (t == typeof(bool)) return bool.Parse(input);
            if (t == typeof(int)) return int.Parse(input);
            if (t == typeof(long)) return long.Parse(input);
            if (t == typeof(double)) return double.Parse(input);
            if (t == typeof(decimal)) return decimal.Parse(input);
            if (t == typeof(DateTime)) return DateTime.Parse(input);
            if (t == typeof(Guid)) return Guid.Parse(input);
            return Convert.ChangeType(input, t);
        }
        catch { return null; }
    }
    public static object HeuristicParse(string s, Type? hint = null)
    {
        var byHint = ConvertTo(s, hint);
        if (byHint != null) return byHint;
        if (bool.TryParse(s, out var b)) return b;
        if (int.TryParse(s, out var i)) return i;
        if (long.TryParse(s, out var l)) return l;
        if (double.TryParse(s, out var d)) return d;
        if (decimal.TryParse(s, out var m)) return m;
        if (DateTime.TryParse(s, out var dt)) return dt;
        if (Guid.TryParse(s, out var g)) return g;
        return s;
    }
}

// 4) Registry with your vendor namespaces
public static class VariableExtensionRegistry
{
    public static readonly List<IExtensionHandler> DefaultHandlers = new()
    {
        new CamundaExtensionHandler(),
        new ZeebeIoExtensionHandler(),
        new FlowableExtensionHandler(),
        new ActivitiExtensionHandler(),
        new VertexBpmnExtensionHandler(),
        // Best-effort generic handlers for those without specific implementations yet:
        new GenericExtensionHandler("http://jbpm.org/bpmn", "jbpm"),
        new GenericExtensionHandler("http://alfresco.org/bpmn", "alfresco"),
        new GenericExtensionHandler("http://osmanthus.io/bpmn", "osmanthus"),
        new GenericExtensionHandler("http://cib.de/schema/bpmn", "cib"),
        // Also include Vertex BPMN core (not ext) if you need
        // new GenericExtensionHandler("http://vertexbpmn.com/bpmn", "vbpmn")
    };

    public static IExtensionHandler? ResolveByNamespace(string ns) =>
        DefaultHandlers.FirstOrDefault(h => h.Namespace == ns);
    public static IEnumerable<IExtensionHandler> All => DefaultHandlers;
}
