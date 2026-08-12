using System.Collections.Immutable;
using System.Xml.Linq;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Serialization;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Namespace prefix entry for strict roundtrip (Phase A).
/// </summary>
public sealed record NamespacePrefix(string Prefix, string Uri, bool Original = true);

/// <summary>
/// Per-element metadata captured for strict ordering & attribute preservation.
/// </summary>
public sealed record ElementMetadata(
    int OrderIndex,
    string ElementName,
    IReadOnlyDictionary<string, string> Attributes,
    bool HadCamundaCollection = false,
    bool HadZeebeInputCollection = false,
    bool HadLoopCardinality = false,
    bool HadCamundaElementVar = false,
    bool HadZeebeInputElement = false,
    bool HadZeebeOutputElement = false
);

/// <summary>
/// Raw metadata captured for strict roundtrip mode (Phase 1/2 + Phase A extensions).
/// RoundtripDirty indicates the model was mutated after parsing and a lossless emit may not be valid.
/// </summary>
public sealed record BpmnRawMetadata(
    IReadOnlyDictionary<string, string>? DefinitionsAttributes = null,
    IReadOnlyDictionary<string, string>? ProcessAttributes = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Incoming = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Outgoing = null,
    IReadOnlyDictionary<string, (string Raw, bool WasCData)>? SequenceFlowConditions = null,
    IReadOnlyDictionary<string, XElement>? RawExtensionElements = null,
    IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawEventDefinitions = null,
    IReadOnlyDictionary<string, XElement>? RawMultiInstance = null,
    IReadOnlyDictionary<string, string>? PriorityAttributeNamespace = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? FlowNodeAttributes = null,
    bool RoundtripDirty = false,
    IReadOnlyList<NamespacePrefix>? NamespacePrefixes = null,
    IReadOnlyDictionary<string, ElementMetadata>? ElementsMetadata = null,
    IReadOnlyList<XElement>? RawGlobalElements = null,
    IReadOnlyList<XElement>? RawArtifacts = null,
    IReadOnlyList<XElement>? RawLanes = null,
    IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawDocumentation = null,
    XElement? RawDiRoot = null,
    IReadOnlySet<string>? PartiallyDirtyElements = null,
    IReadOnlyDictionary<string, string>? GlobalElementKinds = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? VendorNormalizedExtensions = null
);

public static class BpmnRoundtripUtil
{
    public static BpmnModel MarkDirty(BpmnModel model)
    {
        if (model.RawMetadata == null) return model;
        if (model.RawMetadata.RoundtripDirty) return model; // already marked
        var rm = model.RawMetadata with { RoundtripDirty = true };
        return model with { RawMetadata = rm };
    }

    public static BpmnModel MarkDirtyOnAnyChange(BpmnModel model, string elementId)
    {
        var diagnostics = model.Diagnostics.ToList();
        diagnostics.Add($"RT-Dirty:element:{elementId}");
        model = model with { Diagnostics = diagnostics };
        return MarkDirty(model);
    }

    //public static BpmnModel ApplyAttributeChange(BpmnModel model, string elementId, string key, string value)
    //{
    //    // Update task attributes if target is a task
    //    if (model.Tasks.FirstOrDefault(t => t.Id == elementId) is { } task)
    //    {
    //        var attrs = task.Attributes == null ? new Dictionary<string, string>() : new Dictionary<string, string>(task.Attributes);
    //        attrs[key] = value;
    //        if (key == "name") task = task with { Attributes = attrs, Name = value };
    //        else task = task with { Attributes = attrs };
    //        var tasks = model.Tasks.ToList();
    //        var idx = tasks.FindIndex(t => t.Id == elementId);
    //        tasks[idx] = task;
    //        model = model with { Tasks = tasks };
    //    }
    //    // (Could add similar handling for events, gateways etc.)
    //    return MarkDirtyOnAnyChange(model, elementId);
    //}

    //public static BpmnModel ApplyAttributeChangePartial(BpmnModel model, string elementId, string key, string value)
    //{
    //    if (model.RawMetadata == null) return model; // nothing to do
    //    // mutate element (tasks only for now) without setting RoundtripDirty, track element id in PartiallyDirtyElements
    //    if (model.Tasks.FirstOrDefault(t => t.Id == elementId) is { } task)
    //    {
    //        var attrs = task.Attributes == null ? new Dictionary<string, string>() : new Dictionary<string, string>(task.Attributes);
    //        attrs[key] = value;
    //        task = key == "name" ? task with { Attributes = attrs, Name = value } : task with { Attributes = attrs };
    //        var tasks = model.Tasks.ToList();
    //        var idx = tasks.FindIndex(t => t.Id == elementId);
    //        tasks[idx] = task;
    //        var diagnostics = model.Diagnostics.ToList();
    //        diagnostics.Add($"RT-DirtyPartial:element:{elementId}");
    //        var dirtySet = model.RawMetadata.PartiallyDirtyElements != null ? new HashSet<string>(model.RawMetadata.PartiallyDirtyElements) : new HashSet<string>();
    //        dirtySet.Add(elementId);
    //        var rm = model.RawMetadata with { PartiallyDirtyElements = dirtySet }; // keep RoundtripDirty false
    //        model = model with { Tasks = tasks, Diagnostics = diagnostics, RawMetadata = rm };
    //    }
    //    return model;
    //}
}

public record BpmnModel
{
    public string ProcessId { get; set; }
    public string Name { get; set; }
    public IReadOnlyList<Event> Events { get; init; } = Array.Empty<Event>();
    public IReadOnlyList<Gateway> Gateways { get; init; } = [];
    public IReadOnlyList<SubProcess> Subprocesses { get; init; } = [];
    public IReadOnlyList<SequenceFlow> SequenceFlows { get; init; } = [];
    public IReadOnlyList<Task> Tasks { get; init; } = [];
    public IReadOnlyList<DataObject> DataObjects { get; init; } = [];
    public IReadOnlyList<DataObjectReference> DataObjectReferences { get; init; } = [];
    public IReadOnlyList<DataStore> DataStores { get; init; } = [];
    public IReadOnlyList<DataStoreReference> DataStoreReferences { get; init; }
    public IReadOnlyList<Property> Properties { get; init; } = [];
    public IReadOnlyList<InputOutputSpecification> ActivityIo { get; init; } = [];
    public IReadOnlyList<Message> Messages { get; init; } = [];
    public IReadOnlyList<Signal> Signals { get; init; } = [];
    public IReadOnlyList<Error> Errors { get; init; } = [];
    public IReadOnlyList<Escalation> Escalations { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public IReadOnlyList<BpmnShape>? Shapes { get; init; } = [];
    public IReadOnlyList<BpmnEdge>? Edges { get; init; } = [];
    public IReadOnlyList<BpmnLabelStyle>? LabelStyles { get; init; } = [];
    public IReadOnlyList<Participant>? Participants { get; init; } = [];
    public IReadOnlyList<Lane>? Lanes { get; init; } = [];
    public IReadOnlyList<MessageFlow>? MessageFlows { get; init; } = [];
    public IReadOnlyList<TextAnnotation>? TextAnnotations { get; init; } = [];
    public IReadOnlyList<Association>? Associations { get; init; } = [];
    public IReadOnlyList<Group>? Groups { get; init; } = [];
    public Dictionary<string, object>? ProcessVariables { get; init; } = [];
    public IReadOnlyList<Activity>? Activities { get; init; } = [];
    public IReadOnlyList<Definitions> Definitions { get; init; } = [];
    public string Id => ProcessId;
    public Definitions ProcessDefinitions { get; set; } = null;
    public IReadOnlyList<ValidationDiagnostic> ValidationDiagnostics { get; init; } = [];
    public BpmnRawMetadata? RawMetadata { get; init; } = null;
}

[Serializable]
[XmlType("Font", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Font", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
public record Font
{
    public Font()
    {
        Name = string.Empty;
        Size = 0.0;
        IsBold = false;
        IsItalic = false;
        IsUnderline = false;
        IsStrikeThrough = false;
    }

    public Font(string name, double size, bool isBold, bool isItalic, bool isUnderline, bool isStrikeThrough) : this()
    {
        Name = name ?? string.Empty;
        Size = size;
        IsBold = isBold;
        IsItalic = isItalic;
        IsUnderline = isUnderline;
        IsStrikeThrough = isStrikeThrough;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("size")]
    public double Size { get; set; } = 0.0;

    [XmlAttribute("isBold")]
    public bool IsBold { get; set; } = false;

    [XmlAttribute("isItalic")]
    public bool IsItalic { get; set; } = false;

    [XmlAttribute("isUnderline")]
    public bool IsUnderline { get; set; } = false;

    [XmlAttribute("isStrikeThrough")]
    public bool IsStrikeThrough { get; set; } = false;
}


[Serializable]
[XmlType("Point", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Point", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
public record Point
{
    public Point()
    {
        X = 0.0;
        Y = 0.0;
    }

    public Point(double x, double y) : this()
    {
        X = x;
        Y = y;
    }


    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("x")]
    public double X { get; set; } = 0.0;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("y")]
    public double Y { get; set; } = 0.0;
}

[Serializable]
[XmlType("Bounds", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Bounds", Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
public record Bounds
{
    public Bounds()
    {
        X = 0.0;
        Y = 0.0;
        Width = 0.0;
        Height = 0.0;
    }

    public Bounds(double x, double y, double width, double height) : this()
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("x")]
    public double X { get; set; } = 0.0;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("y")]
    public double Y { get; set; } = 0.0;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("width")]
    public double Width { get; set; } = 0.0;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("height")]
    public double Height { get; set; } = 0.0;
}

[Serializable]
[XmlType("DiagramElement", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("DiagramElement", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnEdge))]
[XmlInclude(typeof(BpmnLabel))]
[XmlInclude(typeof(BpmnPlane))]
[XmlInclude(typeof(BpmnShape))]
[XmlInclude(typeof(Edge))]
[XmlInclude(typeof(Label))]
[XmlInclude(typeof(LabeledEdge))]
[XmlInclude(typeof(LabeledShape))]
[XmlInclude(typeof(Node))]
[XmlInclude(typeof(Plane))]
[XmlInclude(typeof(Shape))]
public abstract record DiagramElement
{
    protected DiagramElement()
    {
        Extension = new DiagramElementExtension();
        Id = string.Empty;
        AnyAttributes = new List<XmlAttribute>();
    }

    [XmlElement("extension", Order = 0)]
    public DiagramElementExtension Extension { get; set; } = new DiagramElementExtension();

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlAnyAttribute]
    public List<XmlAttribute> AnyAttributes { get; set; } = new List<XmlAttribute>();
}


[Serializable]
[XmlType("DiagramElementExtension", Namespace = "http://www.omg.org/spec/DD/20100524/DI", AnonymousType = true)]
[DebuggerStepThrough()]
[DesignerCategory("code")]
public record DiagramElementExtension
{
    public DiagramElementExtension()
    {
        AnyElements = new List<XmlElement>();
    }


    [XmlAnyElement(Order = 0)]
    public List<XmlElement> AnyElements { get; set; } = new List<XmlElement>();
}

[Serializable]
[XmlType("Diagram", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Diagram", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnDiagram))]
public abstract record Diagram
{
    protected Diagram()
    {
        Name = string.Empty;
        Documentation = string.Empty;
        Resolution = 0.0;
        Id = string.Empty;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("documentation")]
    public string Documentation { get; set; } = string.Empty;

    [XmlAttribute("resolution")]
    public double Resolution { get; set; } = 0.0;

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;
}


[Serializable]
[XmlType("Node", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Node", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnLabel))]
[XmlInclude(typeof(BpmnPlane))]
[XmlInclude(typeof(BpmnShape))]
[XmlInclude(typeof(Label))]
[XmlInclude(typeof(LabeledShape))]
[XmlInclude(typeof(Plane))]
[XmlInclude(typeof(Shape))]
public abstract record Node : DiagramElement
{
    protected Node() { }
}


[Serializable]
[XmlType("Edge", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Edge", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnEdge))]
[XmlInclude(typeof(LabeledEdge))]
public abstract record Edge : DiagramElement
{
    protected Edge()
    {
        Waypoints = new List<Point>();
    }


    [Required(AllowEmptyStrings = true)]
    [XmlElement("waypoint", Order = 0)]
    public List<Point> Waypoints { get; set; } = new List<Point>();
}

[Serializable]
[XmlType("LabeledEdge", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("LabeledEdge", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnEdge))]
public abstract record LabeledEdge : Edge
{
    protected LabeledEdge() { }
}


[Serializable]
[XmlType("Shape", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Shape", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnShape))]
[XmlInclude(typeof(LabeledShape))]
public abstract record Shape : Node
{
    protected Shape()
    {
        Bounds = new Bounds();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("Bounds", Order = 0, Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Bounds Bounds { get; set; } = new Bounds();
}


[Serializable]
[XmlType("LabeledShape", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("LabeledShape", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnShape))]
public abstract record LabeledShape : Shape
{
    protected LabeledShape() : base() { }
}


[Serializable]
[XmlType("Label", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Label", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnLabel))]
public abstract record Label : Node
{
    protected Label()
    {
        Bounds = new Bounds();
    }

    [XmlElement("Bounds", Order = 0, Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Bounds Bounds { get; set; } = new Bounds();
}


[Serializable]
[XmlType("Plane", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Plane", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnPlane))]
public abstract record Plane : Node
{

    protected Plane()
    {
        DiagramElements = new List<DiagramElement>();
    }


    [XmlElement("BPMNShape", Type = typeof(BpmnShape), Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", Order = 0)]
    [XmlElement("BPMNEdge", Type = typeof(BpmnEdge), Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", Order = 0)]
    [XmlElement("DiagramElement", Order = 0)]
    public List<DiagramElement> DiagramElements { get; set; } = new List<DiagramElement>();
}

[Serializable]
[XmlType("Style", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("Style", Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlInclude(typeof(BpmnLabelStyle))]
public abstract record Style
{
    protected Style()
    {
        Id = string.Empty;
    }

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;
}


[Serializable]
[XmlType("BPMNDiagram", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNDiagram", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnDiagram : Diagram
{
    public BpmnDiagram()
    {
        BpmnPlane = new BpmnPlane();
        BpmnLabelStyles = new List<BpmnLabelStyle>();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("BPMNPlane", Order = 0)]
    public BpmnPlane BpmnPlane { get; set; } = new BpmnPlane();

    [XmlElement("BPMNLabelStyle", Order = 1)]
    public List<BpmnLabelStyle> BpmnLabelStyles { get; set; } = new List<BpmnLabelStyle>();
}

[Serializable]
[XmlType("BPMNPlane", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNPlane", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnPlane : Plane
{
    public BpmnPlane()
    {
        BpmnElement = new XmlQualifiedName();
    }

    [XmlAttribute("bpmnElement")]
    public XmlQualifiedName BpmnElement { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("BPMNLabelStyle", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNLabelStyle", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnLabelStyle : Style
{
    public BpmnLabelStyle()
    {
        Font = new Font();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("Font", Order = 0, Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Font Font { get; set; } = new Font();
}


[Serializable]
[XmlType("BPMNEdge", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNEdge", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnEdge : LabeledEdge
{
    public BpmnEdge()
    {
        BpmnLabel = new BpmnLabel();
        BpmnElement = new XmlQualifiedName();
        SourceElement = new XmlQualifiedName();
        TargetElement = new XmlQualifiedName();
        MessageVisibleKind = MessageVisibleKind.Initiating;
    }

    [XmlElement("BPMNLabel", Order = 0)]
    public BpmnLabel BpmnLabel { get; set; } = new BpmnLabel();

    [XmlAttribute("bpmnElement")]
    public XmlQualifiedName BpmnElement { get; set; } = new XmlQualifiedName();

    [XmlAttribute("sourceElement")]
    public XmlQualifiedName SourceElement { get; set; } = new XmlQualifiedName();

    [XmlAttribute("targetElement")]
    public XmlQualifiedName TargetElement { get; set; } = new XmlQualifiedName();

    [XmlAttribute("messageVisibleKind")]
    public MessageVisibleKind MessageVisibleKind { get; set; } = MessageVisibleKind.Initiating;
}

[Serializable]
[XmlType("BPMNLabel", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNLabel", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnLabel : Label
{
    public BpmnLabel()
    {
        LabelStyle = new XmlQualifiedName();
    }

    [XmlAttribute("labelStyle")]
    public XmlQualifiedName LabelStyle { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("MessageVisibleKind", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public enum MessageVisibleKind
{

    [XmlEnumAttribute("initiating")]
    Initiating,

    [XmlEnumAttribute("non_initiating")]
    NonInitiating,
}


[Serializable]
[XmlType("BPMNShape", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("BPMNShape", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public record BpmnShape : LabeledShape
{
    public BpmnShape()
    {
        BpmnLabel = new BpmnLabel();
        BpmnElement = new XmlQualifiedName();
        IsHorizontal = false;
        IsExpanded = false;
        IsMarkerVisible = false;
        IsMessageVisible = false;
        ParticipantBandKind = ParticipantBandKind.TopInitiating;
        ChoreographyActivityShape = new XmlQualifiedName();
    }

    [XmlElement("BPMNLabel", Order = 0)]
    public BpmnLabel BpmnLabel { get; set; } = new BpmnLabel();

    [XmlAttribute("bpmnElement")]
    public XmlQualifiedName BpmnElement { get; set; } = new XmlQualifiedName();

    [XmlAttribute("isHorizontal")]
    public bool IsHorizontal { get; set; } = false;

    [XmlAttribute("isExpanded")]
    public bool IsExpanded { get; set; } = false;

    [XmlAttribute("isMarkerVisible")]
    public bool IsMarkerVisible { get; set; } = false;

    [XmlAttribute("isMessageVisible")]
    public bool IsMessageVisible { get; set; } = false;

    [XmlAttribute("participantBandKind")]
    public ParticipantBandKind ParticipantBandKind { get; set; } = ParticipantBandKind.TopInitiating;

    [XmlAttribute("choreographyActivityShape")]
    public XmlQualifiedName ChoreographyActivityShape { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("ParticipantBandKind", Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public enum ParticipantBandKind
{

    [XmlEnumAttribute("top_initiating")]
    TopInitiating,

    [XmlEnumAttribute("middle_initiating")]
    MiddleInitiating,

    [XmlEnumAttribute("bottom_initiating")]
    BottomInitiating,

    [XmlEnumAttribute("top_non_initiating")]
    TopNonInitiating,

    [XmlEnumAttribute("middle_non_initiating")]
    MiddleNonInitiating,

    [XmlEnumAttribute("bottom_non_initiating")]
    BottomNonInitiating,
}

[Serializable]
[XmlType("tActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("activity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(AdHocSubProcess))]
[XmlInclude(typeof(BusinessRuleTask))]
[XmlInclude(typeof(CallActivity))]
[XmlInclude(typeof(ManualTask))]
[XmlInclude(typeof(ReceiveTask))]
[XmlInclude(typeof(ScriptTask))]
[XmlInclude(typeof(SendTask))]
[XmlInclude(typeof(ServiceTask))]
[XmlInclude(typeof(SubProcess))]
[XmlInclude(typeof(Task))]
[XmlInclude(typeof(Transaction))]
[XmlInclude(typeof(UserTask))]
public abstract record Activity : FlowNode
{
    protected Activity() : base()
    {
        IoSpecification = new InputOutputSpecification();
        Properties = new List<Property>();
        DataInputAssociations = new List<DataInputAssociation>();
        DataOutputAssociations = new List<DataOutputAssociation>();
        ResourceRoles = new List<ResourceRole>();
        IsForCompensation = false;
        StartQuantity = "1";
        CompletionQuantity = "1";
        Default = string.Empty;
    }

    [XmlElement("ioSpecification", Order = 0)]
    public InputOutputSpecification IoSpecification { get; set; } = new InputOutputSpecification();

    [XmlElement("property", Order = 1)]
    public List<Property> Properties { get; set; } = new List<Property>();

    [XmlElement("dataInputAssociation", Order = 2)]
    public List<DataInputAssociation> DataInputAssociations { get; set; } = new List<DataInputAssociation>();

    [XmlElement("dataOutputAssociation", Order = 3)]
    public List<DataOutputAssociation> DataOutputAssociations { get; set; } = new List<DataOutputAssociation>();

    [XmlElement("performer", Type = typeof(Performer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("humanPerformer", Type = typeof(HumanPerformer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("potentialOwner", Type = typeof(PotentialOwner), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("resourceRole", Order = 4)]
    public List<ResourceRole> ResourceRoles { get; set; } = new List<ResourceRole>();

    [XmlElement("multiInstanceLoopCharacteristics", Type = typeof(MultiInstanceLoopCharacteristics), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 5)]
    [XmlElement("standardLoopCharacteristics", Type = typeof(StandardLoopCharacteristics), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 5)]
    [XmlElement("loopCharacteristics", Order = 5)]
    public LoopCharacteristics LoopCharacteristics { get; set; } = null;

    [DefaultValue(false)]
    [XmlAttribute("isForCompensation")]
    public bool IsForCompensation { get; set; } = false;

    [DefaultValue("1")]
    [XmlAttribute("startQuantity")]
    public string StartQuantity { get; set; } = "1";

    [DefaultValue("1")]
    [XmlAttribute("completionQuantity")]
    public string CompletionQuantity { get; set; } = "1";

    [XmlAttribute("default")]
    public string Default { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tFlowNode", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("flowNode", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Activity))]
[XmlInclude(typeof(AdHocSubProcess))]
[XmlInclude(typeof(BoundaryEvent))]
[XmlInclude(typeof(BusinessRuleTask))]
[XmlInclude(typeof(CallActivity))]
[XmlInclude(typeof(CallChoreography))]
[XmlInclude(typeof(CatchEvent))]
[XmlInclude(typeof(ChoreographyActivity))]
[XmlInclude(typeof(ChoreographyTask))]
[XmlInclude(typeof(ComplexGateway))]
[XmlInclude(typeof(EndEvent))]
[XmlInclude(typeof(Event))]
[XmlInclude(typeof(EventBasedGateway))]
[XmlInclude(typeof(ExclusiveGateway))]
[XmlInclude(typeof(Gateway))]
[XmlInclude(typeof(ImplicitThrowEvent))]
[XmlInclude(typeof(InclusiveGateway))]
[XmlInclude(typeof(IntermediateCatchEvent))]
[XmlInclude(typeof(IntermediateThrowEvent))]
[XmlInclude(typeof(ManualTask))]
[XmlInclude(typeof(ParallelGateway))]
[XmlInclude(typeof(ReceiveTask))]
[XmlInclude(typeof(ScriptTask))]
[XmlInclude(typeof(SendTask))]
[XmlInclude(typeof(ServiceTask))]
[XmlInclude(typeof(StartEvent))]
[XmlInclude(typeof(SubChoreography))]
[XmlInclude(typeof(SubProcess))]
[XmlInclude(typeof(Task))]
[XmlInclude(typeof(ThrowEvent))]
[XmlInclude(typeof(Transaction))]
[XmlInclude(typeof(UserTask))]
public abstract record FlowNode : FlowElement
{
    protected FlowNode() : base()
    {
        Incomings = new List<XmlQualifiedName>();
        Outgoings = new List<XmlQualifiedName>();
    }

    [XmlElement("incoming", Order = 0)]
    public List<XmlQualifiedName> Incomings { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("outgoing", Order = 1)]
    public List<XmlQualifiedName> Outgoings { get; set; } = new List<XmlQualifiedName>();
}


[Serializable]
[XmlType("tFlowElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("flowElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Activity))]
[XmlInclude(typeof(AdHocSubProcess))]
[XmlInclude(typeof(BoundaryEvent))]
[XmlInclude(typeof(BusinessRuleTask))]
[XmlInclude(typeof(CallActivity))]
[XmlInclude(typeof(CallChoreography))]
[XmlInclude(typeof(CatchEvent))]
[XmlInclude(typeof(ChoreographyActivity))]
[XmlInclude(typeof(ChoreographyTask))]
[XmlInclude(typeof(ComplexGateway))]
[XmlInclude(typeof(DataObject))]
[XmlInclude(typeof(DataObjectReference))]
[XmlInclude(typeof(DataStoreReference))]
[XmlInclude(typeof(EndEvent))]
[XmlInclude(typeof(Event))]
[XmlInclude(typeof(EventBasedGateway))]
[XmlInclude(typeof(ExclusiveGateway))]
[XmlInclude(typeof(FlowNode))]
[XmlInclude(typeof(Gateway))]
[XmlInclude(typeof(ImplicitThrowEvent))]
[XmlInclude(typeof(InclusiveGateway))]
[XmlInclude(typeof(IntermediateCatchEvent))]
[XmlInclude(typeof(IntermediateThrowEvent))]
[XmlInclude(typeof(ManualTask))]
[XmlInclude(typeof(ParallelGateway))]
[XmlInclude(typeof(ReceiveTask))]
[XmlInclude(typeof(ScriptTask))]
[XmlInclude(typeof(SendTask))]
[XmlInclude(typeof(SequenceFlow))]
[XmlInclude(typeof(ServiceTask))]
[XmlInclude(typeof(StartEvent))]
[XmlInclude(typeof(SubChoreography))]
[XmlInclude(typeof(SubProcess))]
[XmlInclude(typeof(Task))]
[XmlInclude(typeof(ThrowEvent))]
[XmlInclude(typeof(Transaction))]
[XmlInclude(typeof(UserTask))]
public abstract record FlowElement : BaseElement
{
    protected FlowElement() : base()
    {
        Auditing = new Auditing();
        Monitoring = new Monitoring();
        CategoryValueRefs = new List<XmlQualifiedName>();
        Name = string.Empty;
    }

    [XmlElement("auditing", Order = 0)]
    public Auditing Auditing { get; set; } = new Auditing();

    [XmlElement("monitoring", Order = 1)]
    public Monitoring Monitoring { get; set; } = new Monitoring();

    [XmlElement("categoryValueRef", Order = 2)]
    public List<XmlQualifiedName> CategoryValueRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tBaseElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("baseElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Activity))]
[XmlInclude(typeof(AdHocSubProcess))]
[XmlInclude(typeof(Artifact))]
[XmlInclude(typeof(Assignment))]
[XmlInclude(typeof(Association))]
[XmlInclude(typeof(Auditing))]
[XmlInclude(typeof(BoundaryEvent))]
[XmlInclude(typeof(BusinessRuleTask))]
[XmlInclude(typeof(CallableElement))]
[XmlInclude(typeof(CallActivity))]
[XmlInclude(typeof(CallChoreography))]
[XmlInclude(typeof(CallConversation))]
[XmlInclude(typeof(CancelEventDefinition))]
[XmlInclude(typeof(CatchEvent))]
[XmlInclude(typeof(Category))]
[XmlInclude(typeof(CategoryValue))]
[XmlInclude(typeof(Choreography))]
[XmlInclude(typeof(ChoreographyActivity))]
[XmlInclude(typeof(ChoreographyTask))]
[XmlInclude(typeof(Collaboration))]
[XmlInclude(typeof(CompensateEventDefinition))]
[XmlInclude(typeof(ComplexBehaviorDefinition))]
[XmlInclude(typeof(ComplexGateway))]
[XmlInclude(typeof(ConditionalEventDefinition))]
[XmlInclude(typeof(Conversation))]
[XmlInclude(typeof(ConversationAssociation))]
[XmlInclude(typeof(ConversationLink))]
[XmlInclude(typeof(ConversationNode))]
[XmlInclude(typeof(CorrelationKey))]
[XmlInclude(typeof(CorrelationProperty))]
[XmlInclude(typeof(CorrelationPropertyBinding))]
[XmlInclude(typeof(CorrelationPropertyRetrievalExpression))]
[XmlInclude(typeof(CorrelationSubscription))]
[XmlInclude(typeof(DataAssociation))]
[XmlInclude(typeof(DataInput))]
[XmlInclude(typeof(DataInputAssociation))]
[XmlInclude(typeof(DataObject))]
[XmlInclude(typeof(DataObjectReference))]
[XmlInclude(typeof(DataOutput))]
[XmlInclude(typeof(DataOutputAssociation))]
[XmlInclude(typeof(DataState))]
[XmlInclude(typeof(DataStore))]
[XmlInclude(typeof(DataStoreReference))]
[XmlInclude(typeof(EndEvent))]
[XmlInclude(typeof(EndPoint))]
[XmlInclude(typeof(Error))]
[XmlInclude(typeof(ErrorEventDefinition))]
[XmlInclude(typeof(Escalation))]
[XmlInclude(typeof(EscalationEventDefinition))]
[XmlInclude(typeof(Event))]
[XmlInclude(typeof(EventBasedGateway))]
[XmlInclude(typeof(EventDefinition))]
[XmlInclude(typeof(ExclusiveGateway))]
[XmlInclude(typeof(FlowElement))]
[XmlInclude(typeof(FlowNode))]
[XmlInclude(typeof(Gateway))]
[XmlInclude(typeof(GlobalBusinessRuleTask))]
[XmlInclude(typeof(GlobalChoreographyTask))]
[XmlInclude(typeof(GlobalConversation))]
[XmlInclude(typeof(GlobalManualTask))]
[XmlInclude(typeof(GlobalScriptTask))]
[XmlInclude(typeof(GlobalTask))]
[XmlInclude(typeof(GlobalUserTask))]
[XmlInclude(typeof(Group))]
[XmlInclude(typeof(HumanPerformer))]
[XmlInclude(typeof(ImplicitThrowEvent))]
[XmlInclude(typeof(InclusiveGateway))]
[XmlInclude(typeof(InputOutputBinding))]
[XmlInclude(typeof(InputOutputSpecification))]
[XmlInclude(typeof(InputSet))]
[XmlInclude(typeof(Interface))]
[XmlInclude(typeof(IntermediateCatchEvent))]
[XmlInclude(typeof(IntermediateThrowEvent))]
[XmlInclude(typeof(ItemDefinition))]
[XmlInclude(typeof(Lane))]
[XmlInclude(typeof(LaneSet))]
[XmlInclude(typeof(LinkEventDefinition))]
[XmlInclude(typeof(LoopCharacteristics))]
[XmlInclude(typeof(ManualTask))]
[XmlInclude(typeof(Message))]
[XmlInclude(typeof(MessageEventDefinition))]
[XmlInclude(typeof(MessageFlow))]
[XmlInclude(typeof(MessageFlowAssociation))]
[XmlInclude(typeof(Monitoring))]
[XmlInclude(typeof(MultiInstanceLoopCharacteristics))]
[XmlInclude(typeof(Operation))]
[XmlInclude(typeof(OutputSet))]
[XmlInclude(typeof(ParallelGateway))]
[XmlInclude(typeof(Participant))]
[XmlInclude(typeof(ParticipantAssociation))]
[XmlInclude(typeof(ParticipantMultiplicity))]
[XmlInclude(typeof(PartnerEntity))]
[XmlInclude(typeof(PartnerRole))]
[XmlInclude(typeof(Performer))]
[XmlInclude(typeof(PotentialOwner))]
[XmlInclude(typeof(Process))]
[XmlInclude(typeof(Property))]
[XmlInclude(typeof(ReceiveTask))]
[XmlInclude(typeof(Relationship))]
[XmlInclude(typeof(Rendering))]
[XmlInclude(typeof(Resource))]
[XmlInclude(typeof(ResourceAssignmentExpression))]
[XmlInclude(typeof(ResourceParameter))]
[XmlInclude(typeof(ResourceParameterBinding))]
[XmlInclude(typeof(ResourceRole))]
[XmlInclude(typeof(RootElement))]
[XmlInclude(typeof(ScriptTask))]
[XmlInclude(typeof(SendTask))]
[XmlInclude(typeof(SequenceFlow))]
[XmlInclude(typeof(ServiceTask))]
[XmlInclude(typeof(Signal))]
[XmlInclude(typeof(SignalEventDefinition))]
[XmlInclude(typeof(StandardLoopCharacteristics))]
[XmlInclude(typeof(StartEvent))]
[XmlInclude(typeof(SubChoreography))]
[XmlInclude(typeof(SubConversation))]
[XmlInclude(typeof(SubProcess))]
[XmlInclude(typeof(Task))]
[XmlInclude(typeof(TerminateEventDefinition))]
[XmlInclude(typeof(TextAnnotation))]
[XmlInclude(typeof(ThrowEvent))]
[XmlInclude(typeof(TimerEventDefinition))]
[XmlInclude(typeof(Transaction))]
[XmlInclude(typeof(UserTask))]
public abstract record BaseElement
{
    protected BaseElement()
    {
        Documentations = new List<Documentation>();
        ExtensionElements = new ExtensionElements();
        Id = string.Empty;
        AnyAttributes = new List<XmlAttribute>();
    }

    [XmlElement("documentation", Order = 0)]
    public List<Documentation> Documentations { get; set; } = new List<Documentation>();

    [XmlElement("extensionElements", Order = 1)]
    public ExtensionElements ExtensionElements { get; set; } = new ExtensionElements();

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlAnyAttribute]
    public List<XmlAttribute> AnyAttributes { get; set; } = new List<XmlAttribute>();
}


[Serializable]
[XmlType("tDocumentation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("documentation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Documentation
{
    public Documentation()
    {
        Any = new XmlDocument().CreateElement("Any");
        Id = string.Empty;
        TextFormat = "text/plain";
        Text = new string[0];
    }

    [XmlAnyElement(Order = 0)]
    public XmlElement Any { get; set; } = new XmlDocument().CreateElement("Any");

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [DefaultValue("text/plain")]
    [XmlAttribute("textFormat")]
    public string TextFormat { get; set; } = "text/plain";

    [XmlTextAttribute()]
    public string[] Text { get; set; } = new string[0];
}


[Serializable]
[XmlType("tExtensionElements", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("extensionElements", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ExtensionElements
{
    public ExtensionElements()
    {
        AnyElements = new List<XmlElement>();
    }

    public ExtensionElements(List<XmlElement> anyElements) : this()
    {
        AnyElements = anyElements ?? new List<XmlElement>();
    }


    [XmlAnyElement(Order = 0)]
    public List<XmlElement> AnyElements { get; set; } = new List<XmlElement>();
}

[Serializable]
[XmlType("tAuditing", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("auditing", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Auditing : BaseElement
{
    public Auditing() : base() { }
}


[Serializable]
[XmlType("tMonitoring", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("monitoring", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Monitoring : BaseElement
{
    public Monitoring() : base() { }
}


[Serializable]
[XmlType("tInputOutputSpecification", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("ioSpecification", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record InputOutputSpecification : BaseElement
{
    public InputOutputSpecification() : base()
    {
        DataInputs = new List<DataInput>();
        DataOutputs = new List<DataOutput>();
        InputSets = new List<InputSet>();
        OutputSets = new List<OutputSet>();
    }

    [XmlElement("dataInput", Order = 0)]
    public List<DataInput> DataInputs { get; set; } = new List<DataInput>();

    [XmlElement("dataOutput", Order = 1)]
    public List<DataOutput> DataOutputs { get; set; } = new List<DataOutput>();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("inputSet", Order = 2)]
    public List<InputSet> InputSets { get; set; } = new List<InputSet>();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("outputSet", Order = 3)]
    public List<OutputSet> OutputSets { get; set; } = new List<OutputSet>();
}


[Serializable]
[XmlType("tDataInput", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataInput", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataInput : BaseElement
{
    public DataInput() : base()
    {
        DataState = new DataState();
        Name = string.Empty;
        ItemSubjectRef = new XmlQualifiedName();
        IsCollection = false;
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(false)]
    [XmlAttribute("isCollection")]
    public bool IsCollection { get; set; } = false;
}


[Serializable]
[XmlType("tDataState", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataState", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataState : BaseElement
{
    public DataState() : base()
    {
        Name = string.Empty;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tDataOutput", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataOutput", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataOutput : BaseElement
{
    public DataOutput() : base()
    {
        DataState = new DataState();
        Name = string.Empty;
        ItemSubjectRef = new XmlQualifiedName();
        IsCollection = false;
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(false)]
    [XmlAttribute("isCollection")]
    public bool IsCollection { get; set; } = false;
}


[Serializable]
[XmlType("tInputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("inputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record InputSet : BaseElement
{
    public InputSet() : base()

    {
        DataInputRefs = new List<string>();
        OptionalInputRefs = new List<string>();
        WhileExecutingInputRefs = new List<string>();
        OutputSetRefs = new List<string>();
        Name = string.Empty;
    }

    [XmlElement("dataInputRefs", Order = 0)]
    public List<string> DataInputRefs { get; set; } = new List<string>();

    [XmlElement("optionalInputRefs", Order = 1)]
    public List<string> OptionalInputRefs { get; set; } = new List<string>();

    [XmlElement("whileExecutingInputRefs", Order = 2)]
    public List<string> WhileExecutingInputRefs { get; set; } = new List<string>();

    [XmlElement("outputSetRefs", Order = 3)]
    public List<string> OutputSetRefs { get; set; } = new List<string>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tOutputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("outputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record OutputSet : BaseElement
{
    public OutputSet() : base()
    {
        DataOutputRefs = new List<string>();
        OptionalOutputRefs = new List<string>();
        WhileExecutingOutputRefs = new List<string>();
        InputSetRefs = new List<string>();
        Name = string.Empty;
    }

    [XmlElement("dataOutputRefs", Order = 0)]
    public List<string> DataOutputRefs { get; set; } = new List<string>();

    [XmlElement("optionalOutputRefs", Order = 1)]
    public List<string> OptionalOutputRefs { get; set; } = new List<string>();

    [XmlElement("whileExecutingOutputRefs", Order = 2)]
    public List<string> WhileExecutingOutputRefs { get; set; } = new List<string>();

    [XmlElement("inputSetRefs", Order = 3)]
    public List<string> InputSetRefs { get; set; } = new List<string>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tProperty", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("property", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Property : BaseElement
{
    public Property() : base()
    {
        DataState = new DataState();
        Name = string.Empty;
        ItemSubjectRef = new XmlQualifiedName();
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tDataInputAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataInputAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataInputAssociation : DataAssociation
{
    public DataInputAssociation() : base()
    {

    }

    public DataInputAssociation(List<string> sourceRefs, string targetRef, FormalExpression transformation = null, List<Assignment> assignments = null) : base(sourceRefs, targetRef, transformation, assignments)
    {

    }
}


[Serializable]
[XmlType("tDataAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(DataInputAssociation))]
[XmlInclude(typeof(DataOutputAssociation))]
public record DataAssociation : BaseElement
{
    public DataAssociation() : base()
    {
        SourceRefs = new List<string>();
        TargetRef = string.Empty;
        Transformation = new FormalExpression();
        Assignments = new List<Assignment>();
    }

    public DataAssociation(List<string> sourceRefs, string targetRef, FormalExpression transformation = null, List<Assignment> assignments = null) : this()
    {
        SourceRefs = sourceRefs ?? new List<string>();
        TargetRef = targetRef ?? string.Empty;
        Transformation = transformation ?? new FormalExpression();
        Assignments = assignments ?? new List<Assignment>();
    }

    [XmlElement("sourceRef", Order = 0)]
    public List<string> SourceRefs { get; set; } = new List<string>();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("targetRef", Order = 1)]
    public string TargetRef { get; set; } = string.Empty;

    [XmlElement("transformation", Order = 2)]
    public FormalExpression Transformation { get; set; } = new FormalExpression();

    [XmlElement("assignment", Order = 3)]
    public List<Assignment> Assignments { get; set; } = new List<Assignment>();
}


[Serializable]
[XmlType("tFormalExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("formalExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record FormalExpression : Expression
{
    public FormalExpression() : base()
    {
        Language = string.Empty;
        EvaluatesToTypeRef = new XmlQualifiedName();
    }

    [XmlAttribute("language")]
    public string Language { get; set; } = string.Empty;

    [XmlAttribute("evaluatesToTypeRef")]
    public XmlQualifiedName EvaluatesToTypeRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("expression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(FormalExpression))]
public record Expression : BaseElementWithMixedContent
{
    public Expression() : base()
    {

    }
}


[Serializable]
[XmlType("tBaseElementWithMixedContent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("baseElementWithMixedContent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Expression))]
[XmlInclude(typeof(FormalExpression))]
public abstract record BaseElementWithMixedContent
{
    protected BaseElementWithMixedContent()
    {
        Documentations = new List<Documentation>();
        ExtensionElements = new ExtensionElements();
        Id = string.Empty;
        AnyAttributes = new List<XmlAttribute>();
        Text = new string[0];
    }

    [XmlElement("documentation", Order = 0)]
    public List<Documentation> Documentations { get; set; } = new List<Documentation>();

    [XmlElement("extensionElements", Order = 1)]
    public ExtensionElements ExtensionElements { get; set; } = new ExtensionElements();

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlAnyAttribute]
    public List<XmlAttribute> AnyAttributes { get; set; } = new List<XmlAttribute>();

    [XmlTextAttribute()]
    public string[] Text { get; set; } = new string[0];
}


[Serializable]
[XmlType("tAssignment", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("assignment", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Assignment : BaseElement
{
    public Assignment() : base()
    {
        From = new Expression();
        To = new Expression();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("from", Order = 0)]
    public Expression From { get; set; } = new Expression();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("to", Order = 1)]
    public Expression To { get; set; } = new Expression();
}


[Serializable]
[XmlType("tDataOutputAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataOutputAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataOutputAssociation : DataAssociation
{
    public DataOutputAssociation() : base()
    {

    }

    public DataOutputAssociation(List<string> sourceRefs, string targetRef, FormalExpression transformation = null, List<Assignment> assignments = null) : base(sourceRefs, targetRef, transformation, assignments)
    {

    }
}


[Serializable]
[XmlType("tResourceRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("resourceRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(HumanPerformer))]
[XmlInclude(typeof(Performer))]
[XmlInclude(typeof(PotentialOwner))]
public record ResourceRole : BaseElement
{
    public ResourceRole() : base()
    {
        ResourceRef = new XmlQualifiedName();
        ResourceParameterBindings = new List<ResourceParameterBinding>();
        ResourceAssignmentExpression = new ResourceAssignmentExpression();
        Name = string.Empty;
    }

    [XmlElement("resourceRef", Order = 0)]
    public XmlQualifiedName ResourceRef { get; set; } = new XmlQualifiedName();

    [XmlElement("resourceParameterBinding", Order = 1)]
    public List<ResourceParameterBinding> ResourceParameterBindings { get; set; } = new List<ResourceParameterBinding>();

    [XmlElement("resourceAssignmentExpression", Order = 2)]
    public ResourceAssignmentExpression ResourceAssignmentExpression { get; set; } = new ResourceAssignmentExpression();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tResourceParameterBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("resourceParameterBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ResourceParameterBinding : BaseElement
{
    public ResourceParameterBinding() : base()
    {
        Expression = new Expression();
        ParameterRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("formalExpression", Type = typeof(FormalExpression), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("expression", Order = 0)]
    public Expression Expression { get; set; } = new Expression();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("parameterRef")]
    public XmlQualifiedName ParameterRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tResourceAssignmentExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("resourceAssignmentExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ResourceAssignmentExpression : BaseElement
{
    public ResourceAssignmentExpression() : base()
    {
        Expression = new Expression();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("formalExpression", Type = typeof(FormalExpression), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("expression", Order = 0)]
    public Expression Expression { get; set; } = new Expression();
}


[Serializable]
[XmlType("tLoopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("loopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(MultiInstanceLoopCharacteristics))]
[XmlInclude(typeof(StandardLoopCharacteristics))]
public abstract record LoopCharacteristics : BaseElement
{
    protected LoopCharacteristics() : base() { }



}


[Serializable]
[XmlType("tAdHocSubProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("adHocSubProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record AdHocSubProcess : SubProcess
{
    public AdHocSubProcess() : base()
    {
        CompletionCondition = new Expression();
        CancelRemainingInstances = true;
        Ordering = AdHocOrdering.Parallel;
    }

    [XmlElement("completionCondition", Order = 0)]
    public Expression CompletionCondition { get; set; } = new Expression();

    [DefaultValue(true)]
    [XmlAttribute("cancelRemainingInstances")]
    public bool CancelRemainingInstances { get; set; } = true;

    [XmlAttribute("ordering")]
    public AdHocOrdering Ordering { get; set; } = AdHocOrdering.Parallel;
}


[Serializable]
[XmlType("tSubProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("subProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(AdHocSubProcess))]
[XmlInclude(typeof(Transaction))]
public record SubProcess : Activity
{
    public SubProcess() : base()
    {
        LaneSets = new List<LaneSet>();
        FlowElements = new List<FlowElement>();
        Artifacts = new List<Artifact>();
        TriggeredByEvent = false;
    }

    [XmlElement("laneSet", Order = 0)]
    public List<LaneSet> LaneSets { get; set; } = new List<LaneSet>();

    [XmlElement("adHocSubProcess", Type = typeof(AdHocSubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("boundaryEvent", Type = typeof(BoundaryEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("businessRuleTask", Type = typeof(BusinessRuleTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("callActivity", Type = typeof(CallActivity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("callChoreography", Type = typeof(CallChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("choreographyTask", Type = typeof(ChoreographyTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("complexGateway", Type = typeof(ComplexGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("dataObject", Type = typeof(DataObject), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("dataObjectReference", Type = typeof(DataObjectReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("dataStoreReference", Type = typeof(DataStoreReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("endEvent", Type = typeof(EndEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("event", Type = typeof(Event), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("eventBasedGateway", Type = typeof(EventBasedGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("exclusiveGateway", Type = typeof(ExclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("implicitThrowEvent", Type = typeof(ImplicitThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("inclusiveGateway", Type = typeof(InclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("intermediateCatchEvent", Type = typeof(IntermediateCatchEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("intermediateThrowEvent", Type = typeof(IntermediateThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("manualTask", Type = typeof(ManualTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("parallelGateway", Type = typeof(ParallelGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("receiveTask", Type = typeof(ReceiveTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("scriptTask", Type = typeof(ScriptTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("sendTask", Type = typeof(SendTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("sequenceFlow", Type = typeof(SequenceFlow), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("serviceTask", Type = typeof(ServiceTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("startEvent", Type = typeof(StartEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("subChoreography", Type = typeof(SubChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("subProcess", Type = typeof(SubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("task", Type = typeof(Task), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("transaction", Type = typeof(Transaction), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("userTask", Type = typeof(UserTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("flowElement", Order = 1)]
    public List<FlowElement> FlowElements { get; set; } = new List<FlowElement>();

    [XmlElement("association", Type = typeof(Association), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("group", Type = typeof(Group), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("textAnnotation", Type = typeof(TextAnnotation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("artifact", Order = 2)]
    public List<Artifact> Artifacts { get; set; } = new List<Artifact>();

    [DefaultValue(false)]
    [XmlAttribute("triggeredByEvent")]
    public bool TriggeredByEvent { get; set; } = false;
}


[Serializable]
[XmlType("tLaneSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("laneSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record LaneSet : BaseElement
{
    public LaneSet() : base()
    {
        Lanes = new List<Lane>();
        Name = string.Empty;
    }

    [XmlElement("lane", Order = 0)]
    public List<Lane> Lanes { get; set; } = new List<Lane>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tLane", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("lane", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Lane : BaseElement
{
    public Lane() : base()
    {
        PartitionElement = null;
        FlowNodeRefs = new List<string>();
        ChildLaneSet = new LaneSet();
        Name = string.Empty;
        PartitionElementRef = new XmlQualifiedName();
    }

    [XmlElement("partitionElement", Order = 0)]
    public BaseElement PartitionElement { get; set; } = null;

    [XmlElement("flowNodeRef", Order = 1)]
    public List<string> FlowNodeRefs { get; set; } = new List<string>();

    [XmlElement("childLaneSet", Order = 2)]
    public LaneSet ChildLaneSet { get; set; } = new LaneSet();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("partitionElementRef")]
    public XmlQualifiedName PartitionElementRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tArtifact", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("artifact", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Association))]
[XmlInclude(typeof(Group))]
[XmlInclude(typeof(TextAnnotation))]
public abstract record Artifact : BaseElement
{
    protected Artifact() : base() { }
}


[Serializable]
[XmlType("tAdHocOrdering", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum AdHocOrdering
{
    Parallel,
    Sequential,
}


[Serializable]
[XmlType("tAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("association", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Association : Artifact
{
    public Association() : base()
    {
        SourceRef = new XmlQualifiedName();
        TargetRef = new XmlQualifiedName();
        AssociationDirection = AssociationDirection.None;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("sourceRef")]
    public XmlQualifiedName SourceRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("targetRef")]
    public XmlQualifiedName TargetRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(AssociationDirection.None)]
    [XmlAttribute("associationDirection")]
    public AssociationDirection AssociationDirection { get; set; } = AssociationDirection.None;
}


[Serializable]
[XmlType("tAssociationDirection", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum AssociationDirection
{
    None,
    One,
    Both,
}


[Serializable]
[XmlType("tBoundaryEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("boundaryEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record BoundaryEvent : CatchEvent
{
    public BoundaryEvent() : base()
    {
        CancelActivity = true;
        AttachedToRef = new XmlQualifiedName();
    }

    [DefaultValue(true)]
    [XmlAttribute("cancelActivity")]
    public bool CancelActivity { get; set; } = true;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("attachedToRef")]
    public XmlQualifiedName AttachedToRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCatchEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("catchEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(BoundaryEvent))]
[XmlInclude(typeof(IntermediateCatchEvent))]
[XmlInclude(typeof(StartEvent))]
public abstract record CatchEvent : Event
{
    protected CatchEvent() : base()
    {
        DataOutputs = new List<DataOutput>();
        DataOutputAssociations = new List<DataOutputAssociation>();
        OutputSet = new OutputSet();
        EventDefinitions = new List<EventDefinition>();
        EventDefinitionRefs = new List<XmlQualifiedName>();
        ParallelMultiple = false;
    }

    [XmlElement("dataOutput", Order = 0)]
    public List<DataOutput> DataOutputs { get; set; } = new List<DataOutput>();

    [XmlElement("dataOutputAssociation", Order = 1)]
    public List<DataOutputAssociation> DataOutputAssociations { get; set; } = new List<DataOutputAssociation>();

    [XmlElement("outputSet", Order = 2)]
    public OutputSet OutputSet { get; set; } = new OutputSet();

    [XmlElement("cancelEventDefinition", Type = typeof(CancelEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("compensateEventDefinition", Type = typeof(CompensateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("conditionalEventDefinition", Type = typeof(ConditionalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("errorEventDefinition", Type = typeof(ErrorEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("escalationEventDefinition", Type = typeof(EscalationEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("linkEventDefinition", Type = typeof(LinkEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("messageEventDefinition", Type = typeof(MessageEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("signalEventDefinition", Type = typeof(SignalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("terminateEventDefinition", Type = typeof(TerminateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("timerEventDefinition", Type = typeof(TimerEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("eventDefinition", Order = 3)]
    public List<EventDefinition> EventDefinitions { get; set; } = new List<EventDefinition>();

    [XmlElement("eventDefinitionRef", Order = 4)]
    public List<XmlQualifiedName> EventDefinitionRefs { get; set; } = new List<XmlQualifiedName>();

    [DefaultValue(false)]
    [XmlAttribute("parallelMultiple")]
    public bool ParallelMultiple { get; set; } = false;
}


[Serializable]
[XmlType("tEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("event", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(BoundaryEvent))]
[XmlInclude(typeof(CatchEvent))]
[XmlInclude(typeof(EndEvent))]
[XmlInclude(typeof(ImplicitThrowEvent))]
[XmlInclude(typeof(IntermediateCatchEvent))]
[XmlInclude(typeof(IntermediateThrowEvent))]
[XmlInclude(typeof(StartEvent))]
[XmlInclude(typeof(ThrowEvent))]
public abstract record Event : FlowNode
{
    protected Event() : base()
    {
        Properties = new List<Property>();
    }


    [XmlElement("property", Order = 0)]
    public List<Property> Properties { get; set; } = new List<Property>();
}

[Serializable]
[XmlType("tEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("eventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(CancelEventDefinition))]
[XmlInclude(typeof(CompensateEventDefinition))]
[XmlInclude(typeof(ConditionalEventDefinition))]
[XmlInclude(typeof(ErrorEventDefinition))]
[XmlInclude(typeof(EscalationEventDefinition))]
[XmlInclude(typeof(LinkEventDefinition))]
[XmlInclude(typeof(MessageEventDefinition))]
[XmlInclude(typeof(SignalEventDefinition))]
[XmlInclude(typeof(TerminateEventDefinition))]
[XmlInclude(typeof(TimerEventDefinition))]
public abstract record EventDefinition : RootElement
{
    protected EventDefinition() : base() { }
}


[Serializable]
[XmlType("tRootElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("rootElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(CallableElement))]
[XmlInclude(typeof(CancelEventDefinition))]
[XmlInclude(typeof(Category))]
[XmlInclude(typeof(Choreography))]
[XmlInclude(typeof(Collaboration))]
[XmlInclude(typeof(CompensateEventDefinition))]
[XmlInclude(typeof(ConditionalEventDefinition))]
[XmlInclude(typeof(CorrelationProperty))]
[XmlInclude(typeof(DataStore))]
[XmlInclude(typeof(EndPoint))]
[XmlInclude(typeof(Error))]
[XmlInclude(typeof(ErrorEventDefinition))]
[XmlInclude(typeof(Escalation))]
[XmlInclude(typeof(EscalationEventDefinition))]
[XmlInclude(typeof(EventDefinition))]
[XmlInclude(typeof(GlobalBusinessRuleTask))]
[XmlInclude(typeof(GlobalChoreographyTask))]
[XmlInclude(typeof(GlobalConversation))]
[XmlInclude(typeof(GlobalManualTask))]
[XmlInclude(typeof(GlobalScriptTask))]
[XmlInclude(typeof(GlobalTask))]
[XmlInclude(typeof(GlobalUserTask))]
[XmlInclude(typeof(Interface))]
[XmlInclude(typeof(ItemDefinition))]
[XmlInclude(typeof(LinkEventDefinition))]
[XmlInclude(typeof(Message))]
[XmlInclude(typeof(MessageEventDefinition))]
[XmlInclude(typeof(PartnerEntity))]
[XmlInclude(typeof(PartnerRole))]
[XmlInclude(typeof(Process))]
[XmlInclude(typeof(Resource))]
[XmlInclude(typeof(Signal))]
[XmlInclude(typeof(SignalEventDefinition))]
[XmlInclude(typeof(TerminateEventDefinition))]
[XmlInclude(typeof(TimerEventDefinition))]
public abstract record RootElement : BaseElement
{
    protected RootElement() : base() { }
}


[Serializable]
[XmlType("tBusinessRuleTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("businessRuleTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record BusinessRuleTask : Task
{
    public BusinessRuleTask() : base()
    {
        Implementation = "##unspecified";
    }

    [DefaultValue("##unspecified")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##unspecified";
}


[Serializable]
[XmlType("tTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("task", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(BusinessRuleTask))]
[XmlInclude(typeof(ManualTask))]
[XmlInclude(typeof(ReceiveTask))]
[XmlInclude(typeof(ScriptTask))]
[XmlInclude(typeof(SendTask))]
[XmlInclude(typeof(ServiceTask))]
[XmlInclude(typeof(UserTask))]
public record Task : Activity
{
    public Task() : base() { }
}


[Serializable]
[XmlType("tCallableElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("callableElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(GlobalBusinessRuleTask))]
[XmlInclude(typeof(GlobalManualTask))]
[XmlInclude(typeof(GlobalScriptTask))]
[XmlInclude(typeof(GlobalTask))]
[XmlInclude(typeof(GlobalUserTask))]
[XmlInclude(typeof(Process))]
public record CallableElement : RootElement
{
    public CallableElement() : base()
    {
        SupportedInterfaceRefs = new List<XmlQualifiedName>();
        IoSpecification = new InputOutputSpecification();
        IoBindings = new List<InputOutputBinding>();
        Name = string.Empty;
    }

    [XmlElement("supportedInterfaceRef", Order = 0)]
    public List<XmlQualifiedName> SupportedInterfaceRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("ioSpecification", Order = 1)]
    public InputOutputSpecification IoSpecification { get; set; } = new InputOutputSpecification();

    [XmlElement("ioBinding", Order = 2)]
    public List<InputOutputBinding> IoBindings { get; set; } = new List<InputOutputBinding>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tInputOutputBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("ioBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record InputOutputBinding : BaseElement
{
    public InputOutputBinding() : base()
    {
        OperationRef = new XmlQualifiedName();
        InputDataRef = string.Empty;
        OutputDataRef = string.Empty;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("operationRef")]
    public XmlQualifiedName OperationRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("inputDataRef")]
    public string InputDataRef { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("outputDataRef")]
    public string OutputDataRef { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tCallActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("callActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CallActivity : Activity
{
    public CallActivity() : base()
    {
        CalledElement = new XmlQualifiedName();
    }

    [XmlAttribute("calledElement")]
    public XmlQualifiedName CalledElement { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCallChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("callChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CallChoreography : ChoreographyActivity
{
    public CallChoreography() : base()
    {
        ParticipantAssociations = new List<ParticipantAssociation>();
        CalledChoreographyRef = new XmlQualifiedName();
    }

    [XmlElement("participantAssociation", Order = 0)]
    public List<ParticipantAssociation> ParticipantAssociations { get; set; } = new List<ParticipantAssociation>();

    [XmlAttribute("calledChoreographyRef")]
    public XmlQualifiedName CalledChoreographyRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tChoreographyActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("choreographyActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(CallChoreography))]
[XmlInclude(typeof(ChoreographyTask))]
[XmlInclude(typeof(SubChoreography))]
public abstract record ChoreographyActivity : FlowNode
{
    protected ChoreographyActivity() : base()
    {
        ParticipantRefs = new List<XmlQualifiedName>();
        InitiatingParticipantRef = new XmlQualifiedName();
        CorrelationKeys = new List<CorrelationKey>();
        LoopType = ChoreographyLoopType.None;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("participantRef", Order = 0)]
    public List<XmlQualifiedName> ParticipantRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("correlationKey", Order = 1)]
    public List<CorrelationKey> CorrelationKeys { get; set; } = new List<CorrelationKey>();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("initiatingParticipantRef")]
    public XmlQualifiedName InitiatingParticipantRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(ChoreographyLoopType.None)]
    [XmlAttribute("loopType")]
    public ChoreographyLoopType LoopType { get; set; } = ChoreographyLoopType.None;
}


[Serializable]
[XmlType("tCorrelationKey", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("correlationKey", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CorrelationKey : BaseElement
{
    public CorrelationKey() : base()
    {
        CorrelationPropertyRefs = new List<XmlQualifiedName>();
        Name = string.Empty;
    }

    [XmlElement("correlationPropertyRef", Order = 0)]
    public List<XmlQualifiedName> CorrelationPropertyRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tChoreographyLoopType", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum ChoreographyLoopType
{
    None,
    Standard,
    MultiInstanceSequential,
    MultiInstanceParallel,
}


[Serializable]
[XmlType("tParticipantAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("participantAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ParticipantAssociation : BaseElement
{
    public ParticipantAssociation() : base()
    {
        InnerParticipantRef = new XmlQualifiedName();
        OuterParticipantRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("innerParticipantRef", Order = 0)]
    public XmlQualifiedName InnerParticipantRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("outerParticipantRef", Order = 1)]
    public XmlQualifiedName OuterParticipantRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCallConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("callConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CallConversation : ConversationNode
{
    public CallConversation() : base()
    {
        ParticipantAssociations = new List<ParticipantAssociation>();
        CalledCollaborationRef = new XmlQualifiedName();
    }

    [XmlElement("participantAssociation", Order = 0)]
    public List<ParticipantAssociation> ParticipantAssociations { get; set; } = new List<ParticipantAssociation>();

    [XmlAttribute("calledCollaborationRef")]
    public XmlQualifiedName CalledCollaborationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tConversationNode", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("conversationNode", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(CallConversation))]
[XmlInclude(typeof(Conversation))]
[XmlInclude(typeof(SubConversation))]
public abstract record ConversationNode : BaseElement
{
    protected ConversationNode() : base()
    {
        ParticipantRefs = new List<XmlQualifiedName>();
        MessageFlowRefs = new List<XmlQualifiedName>();
        CorrelationKeys = new List<CorrelationKey>();
        Name = string.Empty;
    }

    [XmlElement("participantRef", Order = 0)]
    public List<XmlQualifiedName> ParticipantRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("messageFlowRef", Order = 1)]
    public List<XmlQualifiedName> MessageFlowRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("correlationKey", Order = 2)]
    public List<CorrelationKey> CorrelationKeys { get; set; } = new List<CorrelationKey>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tCancelEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("cancelEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CancelEventDefinition : EventDefinition
{
    public CancelEventDefinition() : base() { }
}


[Serializable]
[XmlType("tCategory", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("category", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Category : RootElement
{
    public Category() : base()
    {
        CategoryValues = new List<CategoryValue>();
        Name = string.Empty;
    }

    public Category(string name, List<CategoryValue> categoryValues) : this()
    {
        Name = name ?? string.Empty;
        CategoryValues = categoryValues ?? new List<CategoryValue>();
    }

    [XmlElement("categoryValue", Order = 0)]
    public List<CategoryValue> CategoryValues { get; set; } = new List<CategoryValue>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tCategoryValue", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("categoryValue", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CategoryValue : BaseElement
{
    public CategoryValue() : base()
    {
        Value = string.Empty;
    }

    [XmlAttribute("value")]
    public string Value { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("choreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(GlobalChoreographyTask))]
public record Choreography : Collaboration
{
    public Choreography() : base()
    {
        FlowElements = new List<FlowElement>();
    }

    [XmlElement("adHocSubProcess", Type = typeof(AdHocSubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("boundaryEvent", Type = typeof(BoundaryEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("businessRuleTask", Type = typeof(BusinessRuleTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("callActivity", Type = typeof(CallActivity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("callChoreography", Type = typeof(CallChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("choreographyTask", Type = typeof(ChoreographyTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("complexGateway", Type = typeof(ComplexGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataObject", Type = typeof(DataObject), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataObjectReference", Type = typeof(DataObjectReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataStoreReference", Type = typeof(DataStoreReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("endEvent", Type = typeof(EndEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("event", Type = typeof(Event), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("eventBasedGateway", Type = typeof(EventBasedGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("exclusiveGateway", Type = typeof(ExclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("implicitThrowEvent", Type = typeof(ImplicitThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("inclusiveGateway", Type = typeof(InclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("intermediateCatchEvent", Type = typeof(IntermediateCatchEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("intermediateThrowEvent", Type = typeof(IntermediateThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("manualTask", Type = typeof(ManualTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("parallelGateway", Type = typeof(ParallelGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("receiveTask", Type = typeof(ReceiveTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("scriptTask", Type = typeof(ScriptTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("sendTask", Type = typeof(SendTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("sequenceFlow", Type = typeof(SequenceFlow), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("serviceTask", Type = typeof(ServiceTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("startEvent", Type = typeof(StartEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("subChoreography", Type = typeof(SubChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("subProcess", Type = typeof(SubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("task", Type = typeof(Task), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("transaction", Type = typeof(Transaction), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("userTask", Type = typeof(UserTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("flowElement", Order = 0)]
    public List<FlowElement> FlowElements { get; set; } = new List<FlowElement>();
}


[Serializable]
[XmlType("tCollaboration", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("collaboration", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(Choreography))]
[XmlInclude(typeof(GlobalChoreographyTask))]
[XmlInclude(typeof(GlobalConversation))]
public record Collaboration : RootElement
{
    public Collaboration() : base()
    {
        Participants = new List<Participant>();
        MessageFlows = new List<MessageFlow>();
        Artifacts = new List<Artifact>();
        ConversationNodes = new List<ConversationNode>();
        ConversationAssociations = new List<ConversationAssociation>();
        ParticipantAssociations = new List<ParticipantAssociation>();
        MessageFlowAssociations = new List<MessageFlowAssociation>();
        CorrelationKeys = new List<CorrelationKey>();
        ChoreographyRefs = new List<XmlQualifiedName>();
        ConversationLinks = new List<ConversationLink>();
        IsClosed = false;
    }

    [XmlElement("participant", Order = 0)]
    public List<Participant> Participants { get; set; } = new List<Participant>();

    [XmlElement("messageFlow", Order = 1)]
    public List<MessageFlow> MessageFlows { get; set; } = new List<MessageFlow>();

    [XmlElement("association", Type = typeof(Association), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("group", Type = typeof(Group), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("textAnnotation", Type = typeof(TextAnnotation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("artifact", Order = 2)]
    public List<Artifact> Artifacts { get; set; } = new List<Artifact>();

    [XmlElement("callConversation", Type = typeof(CallConversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("conversation", Type = typeof(Conversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("subConversation", Type = typeof(SubConversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("conversationNode", Order = 3)]
    public List<ConversationNode> ConversationNodes { get; set; } = new List<ConversationNode>();

    [XmlElement("conversationAssociation", Order = 4)]
    public List<ConversationAssociation> ConversationAssociations { get; set; } = new List<ConversationAssociation>();

    [XmlElement("participantAssociation", Order = 5)]
    public List<ParticipantAssociation> ParticipantAssociations { get; set; } = new List<ParticipantAssociation>();

    [XmlElement("messageFlowAssociation", Order = 6)]
    public List<MessageFlowAssociation> MessageFlowAssociations { get; set; } = new List<MessageFlowAssociation>();

    [XmlElement("correlationKey", Order = 7)]
    public List<CorrelationKey> CorrelationKeys { get; set; } = new List<CorrelationKey>();

    [XmlElement("choreographyRef", Order = 8)]
    public List<XmlQualifiedName> ChoreographyRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("conversationLink", Order = 9)]
    public List<ConversationLink> ConversationLinks { get; set; } = new List<ConversationLink>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue(false)]
    [XmlAttribute("isClosed")]
    public bool IsClosed { get; set; } = false;
}


[Serializable]
[XmlType("tParticipant", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("participant", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Participant : BaseElement
{
    public Participant() : base()
    {
        InterfaceRefs = new List<XmlQualifiedName>();
        EndPointRefs = new List<XmlQualifiedName>();
        ParticipantMultiplicity = new ParticipantMultiplicity();
        Name = string.Empty;
        ProcessRef = new XmlQualifiedName();
    }

    [XmlElement("interfaceRef", Order = 0)]
    public List<XmlQualifiedName> InterfaceRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("endPointRef", Order = 1)]
    public List<XmlQualifiedName> EndPointRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlElement("participantMultiplicity", Order = 2)]
    public ParticipantMultiplicity ParticipantMultiplicity { get; set; } = new ParticipantMultiplicity();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("processRef")]
    public XmlQualifiedName ProcessRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tParticipantMultiplicity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("participantMultiplicity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ParticipantMultiplicity : BaseElement
{
    public ParticipantMultiplicity() : base()
    {
        Minimum = 0;
        Maximum = 1;
    }

    [DefaultValue(0)]
    [XmlAttribute("minimum")]
    public int Minimum { get; set; } = 0;

    [DefaultValue(1)]
    [XmlAttribute("maximum")]
    public int Maximum { get; set; } = 1;
}


[Serializable]
[XmlType("tMessageFlow", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("messageFlow", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record MessageFlow : BaseElement
{
    public MessageFlow() : base()
    {
        Name = string.Empty;
        SourceRef = new XmlQualifiedName();
        TargetRef = new XmlQualifiedName();
        MessageRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("sourceRef")]
    public XmlQualifiedName SourceRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("targetRef")]
    public XmlQualifiedName TargetRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("messageRef")]
    public XmlQualifiedName MessageRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tConversationAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("conversationAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ConversationAssociation : BaseElement
{
    public ConversationAssociation() : base()
    {
        InnerConversationNodeRef = new XmlQualifiedName();
        OuterConversationNodeRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("innerConversationNodeRef")]
    public XmlQualifiedName InnerConversationNodeRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("outerConversationNodeRef")]
    public XmlQualifiedName OuterConversationNodeRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tMessageFlowAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("messageFlowAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record MessageFlowAssociation : BaseElement
{
    public MessageFlowAssociation() : base()
    {
        InnerMessageFlowRef = new XmlQualifiedName();
        OuterMessageFlowRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("innerMessageFlowRef")]
    public XmlQualifiedName InnerMessageFlowRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("outerMessageFlowRef")]
    public XmlQualifiedName OuterMessageFlowRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tConversationLink", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("conversationLink", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ConversationLink : BaseElement
{
    public ConversationLink() : base()
    {
        Name = string.Empty;
        SourceRef = new XmlQualifiedName();
        TargetRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("sourceRef")]
    public XmlQualifiedName SourceRef { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("targetRef")]
    public XmlQualifiedName TargetRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tChoreographyTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("choreographyTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ChoreographyTask : ChoreographyActivity
{
    public ChoreographyTask() : base()
    {
        MessageFlowRefs = new List<XmlQualifiedName>();
    }


    [Required(AllowEmptyStrings = true)]
    [XmlElement("messageFlowRef", Order = 0)]
    public List<XmlQualifiedName> MessageFlowRefs { get; set; } = new List<XmlQualifiedName>();
}

[Serializable]
[XmlType("tCompensateEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("compensateEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CompensateEventDefinition : EventDefinition
{
    public CompensateEventDefinition() : base()
    {
        WaitForCompletion = false;
        ActivityRef = new XmlQualifiedName();
    }

    [XmlAttribute("waitForCompletion")]
    public bool WaitForCompletion { get; set; } = false;

    [XmlAttribute("activityRef")]
    public XmlQualifiedName ActivityRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tComplexBehaviorDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("complexBehaviorDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ComplexBehaviorDefinition : BaseElement
{
    public ComplexBehaviorDefinition() : base()
    {
        Condition = new FormalExpression();
        Event = new ImplicitThrowEvent();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("condition", Order = 0)]
    public FormalExpression Condition { get; set; } = new FormalExpression();

    [XmlElement("event", Order = 1)]
    public ImplicitThrowEvent Event { get; set; } = new ImplicitThrowEvent();
}


[Serializable]
[XmlType("tImplicitThrowEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("implicitThrowEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ImplicitThrowEvent : ThrowEvent
{
    public ImplicitThrowEvent() : base() { }
}


[Serializable]
[XmlType("tThrowEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("throwEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(EndEvent))]
[XmlInclude(typeof(ImplicitThrowEvent))]
[XmlInclude(typeof(IntermediateThrowEvent))]
public abstract record ThrowEvent : Event
{
    protected ThrowEvent() : base()
    {
        DataInputs = new List<DataInput>();
        DataInputAssociations = new List<DataInputAssociation>();
        InputSet = new InputSet();
        EventDefinitions = new List<EventDefinition>();
        EventDefinitionRefs = new List<XmlQualifiedName>();
    }

    [XmlElement("dataInput", Order = 0)]
    public List<DataInput> DataInputs { get; set; } = new List<DataInput>();

    [XmlElement("dataInputAssociation", Order = 1)]
    public List<DataInputAssociation> DataInputAssociations { get; set; } = new List<DataInputAssociation>();

    [XmlElement("inputSet", Order = 2)]
    public InputSet InputSet { get; set; } = new InputSet();

    [XmlElement("cancelEventDefinition", Type = typeof(CancelEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("compensateEventDefinition", Type = typeof(CompensateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("conditionalEventDefinition", Type = typeof(ConditionalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("errorEventDefinition", Type = typeof(ErrorEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("escalationEventDefinition", Type = typeof(EscalationEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("linkEventDefinition", Type = typeof(LinkEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("messageEventDefinition", Type = typeof(MessageEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("signalEventDefinition", Type = typeof(SignalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("terminateEventDefinition", Type = typeof(TerminateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("timerEventDefinition", Type = typeof(TimerEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 3)]
    [XmlElement("eventDefinition", Order = 3)]
    public List<EventDefinition> EventDefinitions { get; set; } = new List<EventDefinition>();

    [XmlElement("eventDefinitionRef", Order = 4)]
    public List<XmlQualifiedName> EventDefinitionRefs { get; set; } = new List<XmlQualifiedName>();
}


[Serializable]
[XmlType("tComplexGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("complexGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ComplexGateway : Gateway
{
    public ComplexGateway() : base()
    {
        ActivationCondition = new Expression();
        Default = string.Empty;
    }

    [XmlElement("activationCondition", Order = 0)]
    public Expression ActivationCondition { get; set; } = new Expression();

    [XmlAttribute("default")]
    public string Default { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("gateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(ComplexGateway))]
[XmlInclude(typeof(EventBasedGateway))]
[XmlInclude(typeof(ExclusiveGateway))]
[XmlInclude(typeof(InclusiveGateway))]
[XmlInclude(typeof(ParallelGateway))]
public record Gateway : FlowNode
{
    public Gateway() : base()
    {
        GatewayDirection = GatewayDirection.Unspecified;
    }

    [DefaultValue(GatewayDirection.Unspecified)]
    [XmlAttribute("gatewayDirection")]
    public GatewayDirection GatewayDirection { get; set; } = GatewayDirection.Unspecified;
}


[Serializable]
[XmlType("tGatewayDirection", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum GatewayDirection
{
    Unspecified,
    Converging,
    Diverging,
    Mixed,
}


[Serializable]
[XmlType("tConditionalEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("conditionalEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ConditionalEventDefinition : EventDefinition
{
    public ConditionalEventDefinition() : base()
    {
        Condition = new Expression();
    }

    public ConditionalEventDefinition(Expression condition) : this()
    {
        Condition = condition ?? new Expression();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("condition", Order = 0)]
    public Expression Condition { get; set; } = new Expression();
}


[Serializable]
[XmlType("tConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("conversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Conversation : ConversationNode
{
    public Conversation() : base() { }
}


[Serializable]
[XmlType("tCorrelationProperty", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("correlationProperty", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CorrelationProperty : RootElement
{
    public CorrelationProperty() : base()
    {
        CorrelationPropertyRetrievalExpressions = new List<CorrelationPropertyRetrievalExpression>();
        Name = string.Empty;
        Type = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("correlationPropertyRetrievalExpression", Order = 0)]
    public List<CorrelationPropertyRetrievalExpression> CorrelationPropertyRetrievalExpressions { get; set; } = new List<CorrelationPropertyRetrievalExpression>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public XmlQualifiedName Type { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCorrelationPropertyRetrievalExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("correlationPropertyRetrievalExpression", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CorrelationPropertyRetrievalExpression : BaseElement
{
    public CorrelationPropertyRetrievalExpression() : base()
    {
        MessagePath = new FormalExpression();
        MessageRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("messagePath", Order = 0)]
    public FormalExpression MessagePath { get; set; } = new FormalExpression();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("messageRef")]
    public XmlQualifiedName MessageRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCorrelationPropertyBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("correlationPropertyBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CorrelationPropertyBinding : BaseElement
{
    public CorrelationPropertyBinding() : base()
    {
        DataPath = new FormalExpression();
        CorrelationPropertyRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("dataPath", Order = 0)]
    public FormalExpression DataPath { get; set; } = new FormalExpression();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("correlationPropertyRef")]
    public XmlQualifiedName CorrelationPropertyRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tCorrelationSubscription", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("correlationSubscription", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record CorrelationSubscription : BaseElement
{
    public CorrelationSubscription() : base()

    {
        CorrelationPropertyBindings = new List<CorrelationPropertyBinding>();
        CorrelationKeyRef = new XmlQualifiedName();
    }

    [XmlElement("correlationPropertyBinding", Order = 0)]
    public List<CorrelationPropertyBinding> CorrelationPropertyBindings { get; set; } = new List<CorrelationPropertyBinding>();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("correlationKeyRef")]
    public XmlQualifiedName CorrelationKeyRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tDataObject", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataObject", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataObject : FlowElement
{
    public DataObject() : base()
    {
        DataState = new DataState();
        ItemSubjectRef = new XmlQualifiedName();
        IsCollection = false;
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(false)]
    [XmlAttribute("isCollection")]
    public bool IsCollection { get; set; } = false;
}


[Serializable]
[XmlType("tDataObjectReference", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataObjectReference", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataObjectReference : FlowElement
{
    public DataObjectReference() : base()
    {
        DataState = new DataState();
        ItemSubjectRef = new XmlQualifiedName();
        DataObjectRef = string.Empty;
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("dataObjectRef")]
    public string DataObjectRef { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tDataStore", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataStore", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataStore : RootElement
{
    public DataStore() : base()
    {
        DataState = new DataState();
        Name = string.Empty;
        Capacity = string.Empty;
        IsUnlimited = true;
        ItemSubjectRef = new XmlQualifiedName();
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("capacity")]
    public string Capacity { get; set; } = string.Empty;

    [DefaultValue(true)]
    [XmlAttribute("isUnlimited")]
    public bool IsUnlimited { get; set; } = true;

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tDataStoreReference", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("dataStoreReference", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record DataStoreReference : FlowElement
{
    public DataStoreReference() : base()
    {
        DataState = new DataState();
        ItemSubjectRef = new XmlQualifiedName();
        DataStoreRef = new XmlQualifiedName();
    }

    [XmlElement("dataState", Order = 0)]
    public DataState DataState { get; set; } = new DataState();

    [XmlAttribute("itemSubjectRef")]
    public XmlQualifiedName ItemSubjectRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("dataStoreRef")]
    public XmlQualifiedName DataStoreRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tEndEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("endEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record EndEvent : ThrowEvent
{
    public EndEvent() : base() { }
}


[Serializable]
[XmlType("tEndPoint", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("endPoint", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record EndPoint : RootElement
{
    public EndPoint() : base() { }
}


[Serializable]
[XmlType("tError", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("error", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Error : RootElement
{
    public Error() : base()
    {
        Name = string.Empty;
        ErrorCode = string.Empty;
        StructureRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [XmlAttribute("structureRef")]
    public XmlQualifiedName StructureRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tErrorEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("errorEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ErrorEventDefinition : EventDefinition
{
    public ErrorEventDefinition() : base()
    {
        ErrorRef = new XmlQualifiedName();
    }

    [XmlAttribute("errorRef")]
    public XmlQualifiedName ErrorRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tEscalation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("escalation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Escalation : RootElement
{
    public Escalation() : base()
    {
        Name = string.Empty;
        EscalationCode = string.Empty;
        StructureRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("escalationCode")]
    public string EscalationCode { get; set; } = string.Empty;

    [XmlAttribute("structureRef")]
    public XmlQualifiedName StructureRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tEscalationEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("escalationEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record EscalationEventDefinition : EventDefinition
{
    public EscalationEventDefinition() : base()
    {
        EscalationRef = new XmlQualifiedName();
    }

    [XmlAttribute("escalationRef")]
    public XmlQualifiedName EscalationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tEventBasedGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("eventBasedGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record EventBasedGateway : Gateway
{
    public EventBasedGateway() : base()
    {
        Instantiate = false;
        EventGatewayType = EventBasedGatewayType.Exclusive;
    }

    [DefaultValue(false)]
    [XmlAttribute("instantiate")]
    public bool Instantiate { get; set; } = false;

    [DefaultValue(EventBasedGatewayType.Exclusive)]
    [XmlAttribute("eventGatewayType")]
    public EventBasedGatewayType EventGatewayType { get; set; } = EventBasedGatewayType.Exclusive;
}


[Serializable]
[XmlType("tEventBasedGatewayType", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum EventBasedGatewayType
{
    Exclusive,
    Parallel,
}


[Serializable]
[XmlType("tExclusiveGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("exclusiveGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ExclusiveGateway : Gateway
{
    public ExclusiveGateway() : base()
    {
        Default = string.Empty;
    }

    [XmlAttribute("default")]
    public string Default { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tExtension", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("extension", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Extension
{
    public Extension()
    {
        Documentations = new List<Documentation>();
        Definition = new XmlQualifiedName();
        MustUnderstand = false;
    }

    [XmlElement("documentation", Order = 0)]
    public List<Documentation> Documentations { get; set; } = new List<Documentation>();

    [XmlAttribute("definition")]
    public XmlQualifiedName Definition { get; set; } = new XmlQualifiedName();

    [DefaultValue(false)]
    [XmlAttribute("mustUnderstand")]
    public bool MustUnderstand { get; set; } = false;
}


[Serializable]
[XmlType("tGlobalBusinessRuleTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalBusinessRuleTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalBusinessRuleTask : GlobalTask
{
    public GlobalBusinessRuleTask() : base()
    {
        Implementation = "##unspecified";
    }

    [DefaultValue("##unspecified")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##unspecified";
}


[Serializable]
[XmlType("tGlobalTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(GlobalBusinessRuleTask))]
[XmlInclude(typeof(GlobalManualTask))]
[XmlInclude(typeof(GlobalScriptTask))]
[XmlInclude(typeof(GlobalUserTask))]
public record GlobalTask : CallableElement
{
    public GlobalTask() : base()
    {
        ResourceRoles = new List<ResourceRole>();
    }

    [XmlElement("performer", Type = typeof(Performer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("humanPerformer", Type = typeof(HumanPerformer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("potentialOwner", Type = typeof(PotentialOwner), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("resourceRole", Order = 0)]
    public List<ResourceRole> ResourceRoles { get; set; } = new List<ResourceRole>();
}


[Serializable]
[XmlType("tGlobalChoreographyTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalChoreographyTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalChoreographyTask : Choreography
{
    public GlobalChoreographyTask() : base()
    {
        InitiatingParticipantRef = new XmlQualifiedName();
    }

    [XmlAttribute("initiatingParticipantRef")]
    public XmlQualifiedName InitiatingParticipantRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tGlobalConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalConversation : Collaboration
{
    public GlobalConversation() : base() { }
}


[Serializable]
[XmlType("tGlobalManualTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalManualTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalManualTask : GlobalTask
{
    public GlobalManualTask() : base() { }
}


[Serializable]
[XmlType("tGlobalScriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalScriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalScriptTask : GlobalTask
{
    public GlobalScriptTask() : base()
    {
        Script = new Script();
        ScriptLanguage = string.Empty;
    }

    [XmlElement("script", Order = 0)]
    public Script Script { get; set; } = new Script();

    [XmlAttribute("scriptLanguage")]
    public string ScriptLanguage { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tScript", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("script", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Script
{
    public Script()
    {
        Any = new XmlDocument().CreateElement("Any");
        Text = new string[0];
    }

    public Script(string[] Text, XmlElement Any)
    {
        this.Text = Text ?? new string[0];
        this.Any = Any ?? new XmlDocument().CreateElement("Any");
    }


    [XmlAnyElement(Order = 0)]
    public XmlElement Any { get; set; } = new XmlDocument().CreateElement("Any");

    [XmlTextAttribute()]
    public string[] Text { get; set; } = new string[0];
}

[Serializable]
[XmlType("tGlobalUserTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("globalUserTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record GlobalUserTask : GlobalTask
{
    public GlobalUserTask() : base()
    {
        Renderings = new List<Rendering>();
        Implementation = "##unspecified";
    }

    [XmlElement("rendering", Order = 0)]
    public List<Rendering> Renderings { get; set; } = new List<Rendering>();

    [DefaultValue("##unspecified")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##unspecified";
}


[Serializable]
[XmlType("tRendering", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("rendering", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Rendering : BaseElement
{
    public Rendering() : base() { }
}


[Serializable]
[XmlType("tGroup", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("group", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Group : Artifact
{
    public Group() : base()
    {
        CategoryValueRef = new XmlQualifiedName();
    }

    [XmlAttribute("categoryValueRef")]
    public XmlQualifiedName CategoryValueRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tHumanPerformer", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("humanPerformer", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(PotentialOwner))]
public record HumanPerformer : Performer
{
    public HumanPerformer() : base() { }
}


[Serializable]
[XmlType("tPerformer", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("performer", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlInclude(typeof(HumanPerformer))]
[XmlInclude(typeof(PotentialOwner))]
public record Performer : ResourceRole
{
    public Performer() : base() { }
}


[Serializable]
[XmlType("tInclusiveGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("inclusiveGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record InclusiveGateway : Gateway
{
    public InclusiveGateway() : base()
    {
        Default = string.Empty;
    }

    [XmlAttribute("default")]
    public string Default { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tInterface", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("interface", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Interface : RootElement
{
    public Interface() : base()
    {
        Operations = new List<Operation>();
        Name = string.Empty;
        ImplementationRef = new XmlQualifiedName();
    }

    public Interface(string Name, List<Operation> ops) : this()
    {
        this.Name = Name ?? string.Empty;
        Operations = ops ?? new List<Operation>();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("operation", Order = 0)]
    public List<Operation> Operations { get; set; } = new List<Operation>();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("implementationRef")]
    public XmlQualifiedName ImplementationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tOperation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("operation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Operation : BaseElement
{
    public Operation() : base()
    {
        InMessageRef = new XmlQualifiedName();
        OutMessageRef = new XmlQualifiedName();
        ErrorRefs = new List<XmlQualifiedName>();
        Name = string.Empty;
        ImplementationRef = new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("inMessageRef", Order = 0)]
    public XmlQualifiedName InMessageRef { get; set; } = new XmlQualifiedName();

    [XmlElement("outMessageRef", Order = 1)]
    public XmlQualifiedName OutMessageRef { get; set; } = new XmlQualifiedName();

    [XmlElement("errorRef", Order = 2)]
    public List<XmlQualifiedName> ErrorRefs { get; set; } = new List<XmlQualifiedName>();
    public Operation(string Name, XmlQualifiedName InMessageRef) : this()
    {
        this.Name = Name ?? string.Empty;
        this.InMessageRef = InMessageRef ?? new XmlQualifiedName();
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("implementationRef")]
    public XmlQualifiedName ImplementationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tIntermediateCatchEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("intermediateCatchEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record IntermediateCatchEvent : CatchEvent
{
    public IntermediateCatchEvent() : base() { }
}


[Serializable]
[XmlType("tIntermediateThrowEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("intermediateThrowEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record IntermediateThrowEvent : ThrowEvent
{
    public IntermediateThrowEvent() : base() { }
}


[Serializable]
[XmlType("tItemDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("itemDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ItemDefinition : RootElement
{
    public ItemDefinition() : base()
    {
        StructureRef = new XmlQualifiedName();
        IsCollection = false;
        ItemKind = ItemKind.Information;
    }

    [XmlAttribute("structureRef")]
    public XmlQualifiedName StructureRef { get; set; } = new XmlQualifiedName();

    [DefaultValue(false)]
    [XmlAttribute("isCollection")]
    public bool IsCollection { get; set; } = false;

    [DefaultValue(ItemKind.Information)]
    [XmlAttribute("itemKind")]
    public ItemKind ItemKind { get; set; } = ItemKind.Information;
}


[Serializable]
[XmlType("tItemKind", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum ItemKind
{
    Information,
    Physical,
}


[Serializable]
[XmlType("tLinkEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("linkEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record LinkEventDefinition : EventDefinition
{
    public LinkEventDefinition() : base()


    {
        Sources = new List<XmlQualifiedName>();
        Target = new XmlQualifiedName();
        Name = string.Empty;
    }

    [XmlElement("source", Order = 0)]
    public List<XmlQualifiedName> Sources { get; set; } = new List<XmlQualifiedName>();

    public LinkEventDefinition(string v, List<XmlQualifiedName> sources, XmlQualifiedName target) : this()
    {
        Name = v ?? string.Empty;
        Sources = sources ?? new List<XmlQualifiedName>();
        Target = target ?? new XmlQualifiedName();
    }

    [XmlElement("target", Order = 1)]
    public XmlQualifiedName Target { get; set; } = new XmlQualifiedName();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tManualTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("manualTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ManualTask : Task
{
    public ManualTask() : base() { }
}


[Serializable]
[XmlType("tMessage", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("message", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Message : RootElement
{
    public Message() : base()
    {
        Name = string.Empty;
        ItemRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("itemRef")]
    public XmlQualifiedName ItemRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tMessageEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("messageEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record MessageEventDefinition : EventDefinition
{
    public MessageEventDefinition() : base()
    {
        OperationRef = new XmlQualifiedName();
        MessageRef = new XmlQualifiedName();
    }

    [XmlElement("operationRef", Order = 0)]
    public XmlQualifiedName OperationRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("messageRef")]
    public XmlQualifiedName MessageRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tMultiInstanceLoopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("multiInstanceLoopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record MultiInstanceLoopCharacteristics : LoopCharacteristics
{
    public MultiInstanceLoopCharacteristics() : base()
    {
        LoopCardinality = new Expression();
        LoopDataInputRef = new XmlQualifiedName();
        LoopDataOutputRef = new XmlQualifiedName();
        InputDataItem = new DataInput();
        OutputDataItem = new DataOutput();
        ComplexBehaviorDefinitions = new List<ComplexBehaviorDefinition>();
        CompletionCondition = new Expression();
        IsSequential = false;
        Behavior = MultiInstanceFlowCondition.All;
        OneBehaviorEventRef = new XmlQualifiedName();
        NoneBehaviorEventRef = new XmlQualifiedName();
    }

    [XmlElement("loopCardinality", Order = 0)]
    public Expression LoopCardinality { get; set; } = new Expression();

    [XmlElement("loopDataInputRef", Order = 1)]
    public XmlQualifiedName LoopDataInputRef { get; set; } = new XmlQualifiedName();

    [XmlElement("loopDataOutputRef", Order = 2)]
    public XmlQualifiedName LoopDataOutputRef { get; set; } = new XmlQualifiedName();

    [XmlElement("inputDataItem", Order = 3)]
    public DataInput InputDataItem { get; set; } = new DataInput();

    [XmlElement("outputDataItem", Order = 4)]
    public DataOutput OutputDataItem { get; set; } = new DataOutput();

    [XmlElement("complexBehaviorDefinition", Order = 5)]
    public List<ComplexBehaviorDefinition> ComplexBehaviorDefinitions { get; set; } = new List<ComplexBehaviorDefinition>();


    [XmlElement("completionCondition", Order = 6)]
    public Expression CompletionCondition { get; set; } = new Expression();

    [DefaultValue(false)]
    [XmlAttribute("isSequential")]
    public bool IsSequential { get; set; } = false;

    [DefaultValue(MultiInstanceFlowCondition.All)]
    [XmlAttribute("behavior")]
    public MultiInstanceFlowCondition Behavior { get; set; } = MultiInstanceFlowCondition.All;

    [XmlAttribute("oneBehaviorEventRef")]
    public XmlQualifiedName OneBehaviorEventRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("noneBehaviorEventRef")]
    public XmlQualifiedName NoneBehaviorEventRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tMultiInstanceFlowCondition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum MultiInstanceFlowCondition
{
    None,
    One,
    All,
    Complex,
}


[Serializable]
[XmlType("tParallelGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("parallelGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ParallelGateway : Gateway
{
    public ParallelGateway() : base() { }
}


[Serializable]
[XmlType("tPartnerEntity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("partnerEntity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record PartnerEntity : RootElement
{
    public PartnerEntity() : base()
    {
        ParticipantRefs = new List<XmlQualifiedName>();
        Name = string.Empty;
    }

    [XmlElement("participantRef", Order = 0)]
    public List<XmlQualifiedName> ParticipantRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tPartnerRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("partnerRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record PartnerRole : RootElement
{
    public PartnerRole() : base()
    {
        ParticipantRefs = new List<XmlQualifiedName>();
        Name = string.Empty;
    }

    [XmlElement("participantRef", Order = 0)]
    public List<XmlQualifiedName> ParticipantRefs { get; set; } = new List<XmlQualifiedName>();

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tPotentialOwner", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("potentialOwner", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record PotentialOwner : HumanPerformer
{
    public PotentialOwner() : base() { }
}


[Serializable]
[XmlType("tProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("process", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Process : CallableElement
{
    public Process() : base()
    {
        Auditing = new Auditing();
        Monitoring = new Monitoring();
        Properties = new List<Property>();
        LaneSets = new List<LaneSet>();
        FlowElements = new List<FlowElement>();
        Artifacts = new List<Artifact>();
        ResourceRoles = new List<ResourceRole>();
        CorrelationSubscriptions = new List<CorrelationSubscription>();
        Supports = new List<XmlQualifiedName>();
        ProcessType = ProcessType.None;
        IsClosed = false;
        IsExecutable = false;
        IsExecutableValueSpecified = false;
        DefinitionalCollaborationRef = new XmlQualifiedName();
    }

    [XmlElement("auditing", Order = 0)]
    public Auditing Auditing { get; set; } = new Auditing();

    [XmlElement("monitoring", Order = 1)]
    public Monitoring Monitoring { get; set; } = new Monitoring();

    [XmlElement("property", Order = 2)]
    public List<Property> Properties { get; set; } = new List<Property>();

    [XmlElement("laneSet", Order = 3)]
    public List<LaneSet> LaneSets { get; set; } = new List<LaneSet>();

    [XmlElement("adHocSubProcess", Type = typeof(AdHocSubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("boundaryEvent", Type = typeof(BoundaryEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("businessRuleTask", Type = typeof(BusinessRuleTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("callActivity", Type = typeof(CallActivity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("callChoreography", Type = typeof(CallChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("choreographyTask", Type = typeof(ChoreographyTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("complexGateway", Type = typeof(ComplexGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("dataObject", Type = typeof(DataObject), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("dataObjectReference", Type = typeof(DataObjectReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("dataStoreReference", Type = typeof(DataStoreReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("endEvent", Type = typeof(EndEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("event", Type = typeof(Event), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("eventBasedGateway", Type = typeof(EventBasedGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("exclusiveGateway", Type = typeof(ExclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("implicitThrowEvent", Type = typeof(ImplicitThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("inclusiveGateway", Type = typeof(InclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("intermediateCatchEvent", Type = typeof(IntermediateCatchEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("intermediateThrowEvent", Type = typeof(IntermediateThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("manualTask", Type = typeof(ManualTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("parallelGateway", Type = typeof(ParallelGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("receiveTask", Type = typeof(ReceiveTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("scriptTask", Type = typeof(ScriptTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("sendTask", Type = typeof(SendTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("sequenceFlow", Type = typeof(SequenceFlow), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("serviceTask", Type = typeof(ServiceTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("startEvent", Type = typeof(StartEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("subChoreography", Type = typeof(SubChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("subProcess", Type = typeof(SubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("task", Type = typeof(Task), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("transaction", Type = typeof(Transaction), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("userTask", Type = typeof(UserTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 4)]
    [XmlElement("flowElement", Order = 4)]
    public List<FlowElement> FlowElements { get; set; } = new List<FlowElement>();

    [XmlElement("association", Type = typeof(Association), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 5)]
    [XmlElement("group", Type = typeof(Group), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 5)]
    [XmlElement("textAnnotation", Type = typeof(TextAnnotation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 5)]
    [XmlElement("artifact", Order = 5)]
    public List<Artifact> Artifacts { get; set; } = new List<Artifact>();

    [XmlElement("performer", Type = typeof(Performer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 6)]
    [XmlElement("humanPerformer", Type = typeof(HumanPerformer), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 6)]
    [XmlElement("potentialOwner", Type = typeof(PotentialOwner), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 6)]
    [XmlElement("resourceRole", Order = 6)]
    public List<ResourceRole> ResourceRoles { get; set; } = new List<ResourceRole>();

    [XmlElement("correlationSubscription", Order = 7)]
    public List<CorrelationSubscription> CorrelationSubscriptions { get; set; } = new List<CorrelationSubscription>();

    [XmlElement("supports", Order = 8)]
    public List<XmlQualifiedName> Supports { get; set; } = new List<XmlQualifiedName>();

    [DefaultValue(ProcessType.None)]
    [XmlAttribute("processType")]
    public ProcessType ProcessType { get; set; } = ProcessType.None;

    [DefaultValue(false)]
    [XmlAttribute("isClosed")]
    public bool IsClosed { get; set; } = false;

    [XmlAttribute("isExecutable")]
    public bool IsExecutable { get; set; } = false;

    [XmlIgnore]
    public bool IsExecutableValueSpecified { get; set; } = false;

    [XmlAttribute("definitionalCollaborationRef")]
    public XmlQualifiedName DefinitionalCollaborationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tProcessType", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum ProcessType
{
    None,
    Public,
    Private,
}


[Serializable]
[XmlType("tReceiveTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("receiveTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ReceiveTask : Task
{
    public ReceiveTask() : base()
    {
        Implementation = "##WebService";
        Instantiate = false;
        MessageRef = new XmlQualifiedName();
        OperationRef = new XmlQualifiedName();
    }

    [DefaultValue("##WebService")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##WebService";

    [DefaultValue(false)]
    [XmlAttribute("instantiate")]
    public bool Instantiate { get; set; } = false;

    [XmlAttribute("messageRef")]
    public XmlQualifiedName MessageRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("operationRef")]
    public XmlQualifiedName OperationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tRelationship", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("relationship", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Relationship : BaseElement
{
    public Relationship() : base()
    {
        Sources = new List<XmlQualifiedName>();
        Targets = new List<XmlQualifiedName>();
        Type = string.Empty;
        Direction = RelationshipDirection.None;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlElement("source", Order = 0)]
    public List<XmlQualifiedName> Sources { get; set; } = new List<XmlQualifiedName>();

    [Required(AllowEmptyStrings = true)]
    [XmlElement("target", Order = 1)]
    public List<XmlQualifiedName> Targets { get; set; } = new List<XmlQualifiedName>();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("direction")]
    public RelationshipDirection Direction { get; set; } = RelationshipDirection.None;
}


[Serializable]
[XmlType("tRelationshipDirection", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum RelationshipDirection
{
    None,
    Forward,
    Backward,
    Both,
}


[Serializable]
[XmlType("tResource", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("resource", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Resource : RootElement
{
    public Resource() : base()
    {
        ResourceParameters = new List<ResourceParameter>();
        Name = string.Empty;
    }

    public Resource(string name, List<ResourceParameter> resourceParameters) : this()
    {
        Name = name ?? string.Empty;
        ResourceParameters = resourceParameters ?? new List<ResourceParameter>();
    }

    [XmlElement("resourceParameter", Order = 0)]
    public List<ResourceParameter> ResourceParameters { get; set; } = new List<ResourceParameter>();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tResourceParameter", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("resourceParameter", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ResourceParameter : BaseElement
{
    public ResourceParameter() : base()
    {
        Name = string.Empty;
        Type = new XmlQualifiedName();
        IsRequired = false;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public XmlQualifiedName Type { get; set; } = new XmlQualifiedName();

    [XmlAttribute("isRequired")]
    public bool IsRequired { get; set; } = false;
}


[Serializable]
[XmlType("tScriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("scriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ScriptTask : Task
{
    public ScriptTask() : base()
    {
        Script = new Script();
        ScriptFormat = string.Empty;
    }

    [XmlElement("script", Order = 0)]
    public Script Script { get; set; } = new Script();

    [XmlAttribute("scriptFormat")]
    public string ScriptFormat { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tSendTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("sendTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record SendTask : Task
{
    public SendTask() : base()
    {
        Implementation = "##WebService";
        MessageRef = new XmlQualifiedName();
        OperationRef = new XmlQualifiedName();
    }

    [DefaultValue("##WebService")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##WebService";

    [XmlAttribute("messageRef")]
    public XmlQualifiedName MessageRef { get; set; } = new XmlQualifiedName();

    [XmlAttribute("operationRef")]
    public XmlQualifiedName OperationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tSequenceFlow", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("sequenceFlow", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record SequenceFlow : FlowElement
{
    public SequenceFlow() : base()
    {
        ConditionExpression = new Expression();
        SourceRef = string.Empty;
        TargetRef = string.Empty;
        IsImmediate = false;
    }

    [XmlElement("conditionExpression", Order = 0)]
    public Expression ConditionExpression { get; set; } = new Expression();

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("sourceRef")]
    public string SourceRef { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("targetRef")]
    public string TargetRef { get; set; } = string.Empty;

    [XmlAttribute("isImmediate")]
    public bool IsImmediate { get; set; } = false;
}


[Serializable]
[XmlType("tServiceTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("serviceTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record ServiceTask : Task
{
    public ServiceTask() : base()
    {
        Implementation = "##WebService";
        OperationRef = new XmlQualifiedName();
    }

    [DefaultValue("##WebService")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##WebService";

    [XmlAttribute("operationRef")]
    public XmlQualifiedName OperationRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tSignal", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("signal", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Signal : RootElement
{
    public Signal() : base()
    {
        Name = string.Empty;
        StructureRef = new XmlQualifiedName();
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("structureRef")]
    public XmlQualifiedName StructureRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tSignalEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("signalEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record SignalEventDefinition : EventDefinition
{
    public SignalEventDefinition() : base()
    {
        SignalRef = new XmlQualifiedName();
    }

    [XmlAttribute("signalRef")]
    public XmlQualifiedName SignalRef { get; set; } = new XmlQualifiedName();
}


[Serializable]
[XmlType("tStandardLoopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("standardLoopCharacteristics", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record StandardLoopCharacteristics : LoopCharacteristics
{
    public StandardLoopCharacteristics() : base()
    {
        LoopCondition = new Expression();
        TestBefore = false;
        LoopMaximum = string.Empty;
    }

    [XmlElement("loopCondition", Order = 0)]
    public Expression LoopCondition { get; set; } = new Expression();

    [DefaultValue(false)]
    [XmlAttribute("testBefore")]
    public bool TestBefore { get; set; } = false;

    [XmlAttribute("loopMaximum")]
    public string LoopMaximum { get; set; } = string.Empty;
}


[Serializable]
[XmlType("tStartEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("startEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record StartEvent : CatchEvent
{
    public StartEvent() : base()
    {
        IsInterrupting = true;
    }

    [DefaultValue(true)]
    [XmlAttribute("isInterrupting")]
    public bool IsInterrupting { get; set; } = true;
}

[Serializable]
[XmlType("tSubChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("subChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record SubChoreography : ChoreographyActivity
{
    public SubChoreography()
    {
        FlowElements = new List<FlowElement>();
        Artifacts = new List<Artifact>();
    }

    [XmlElement("activity", Type = typeof(Activity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("adHocSubProcess", Type = typeof(AdHocSubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("boundaryEvent", Type = typeof(BoundaryEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("businessRuleTask", Type = typeof(BusinessRuleTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("callActivity", Type = typeof(CallActivity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("callChoreography", Type = typeof(CallChoreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("choreographyTask", Type = typeof(ChoreographyTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("complexGateway", Type = typeof(ComplexGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataObject", Type = typeof(DataObject), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataObjectReference", Type = typeof(DataObjectReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("dataStoreReference", Type = typeof(DataStoreReference), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("endEvent", Type = typeof(EndEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("event", Type = typeof(Event), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("eventBasedGateway", Type = typeof(EventBasedGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("exclusiveGateway", Type = typeof(ExclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("gateway", Type = typeof(Gateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("implicitThrowEvent", Type = typeof(ImplicitThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("inclusiveGateway", Type = typeof(InclusiveGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("intermediateCatchEvent", Type = typeof(IntermediateCatchEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("intermediateThrowEvent", Type = typeof(IntermediateThrowEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("manualTask", Type = typeof(ManualTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("parallelGateway", Type = typeof(ParallelGateway), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("receiveTask", Type = typeof(ReceiveTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("scriptTask", Type = typeof(ScriptTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("sendTask", Type = typeof(SendTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("sequenceFlow", Type = typeof(SequenceFlow), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("serviceTask", Type = typeof(ServiceTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("startEvent", Type = typeof(StartEvent), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("subProcess", Type = typeof(SubProcess), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("task", Type = typeof(Task), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("transaction", Type = typeof(Transaction), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("userTask", Type = typeof(UserTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("flowElement", Order = 0)]
    public List<FlowElement> FlowElements { get; set; } = new List<FlowElement>();

    [XmlElement("association", Type = typeof(Association), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("group", Type = typeof(Group), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("textAnnotation", Type = typeof(TextAnnotation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 1)]
    [XmlElement("artifact", Order = 1)]
    public List<Artifact> Artifacts { get; set; } = new List<Artifact>();
}

[Serializable]
[XmlType("tSubConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("subConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record SubConversation : ConversationNode
{

    public SubConversation()
    {
        ConversationNodes = new List<ConversationNode>();
    }

    [XmlElement("callConversation", Type = typeof(CallConversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("conversation", Type = typeof(Conversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("subConversation", Type = typeof(SubConversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 0)]
    [XmlElement("conversationNode", Order = 0)]
    public List<ConversationNode> ConversationNodes { get; set; } = new List<ConversationNode>();
}

[Serializable]
[XmlType("tTerminateEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("terminateEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record TerminateEventDefinition : EventDefinition
{
    public TerminateEventDefinition() { }
}

[Serializable]
[XmlType("tTextAnnotation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("textAnnotation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record TextAnnotation : Artifact
{
    public TextAnnotation()
    {
        Text = new TText();
        TextFormat = "text/plain";
    }

    [XmlElement("text", Order = 0)]
    public TText Text { get; set; } = new TText();

    [DefaultValue("text/plain")]
    [XmlAttribute("textFormat")]
    public string TextFormat { get; set; } = "text/plain";
}

[Serializable]
[XmlType("tText", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("text", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record TText
{
    public TText()
    {
        Any = new XmlDocument().CreateElement("Any");
        Text = new string[0];
    }

    [XmlAnyElement(Order = 0)]
    public XmlElement Any { get; set; } = new XmlDocument().CreateElement("Any");

    [XmlTextAttribute()]
    public string[] Text { get; set; } = new string[0];
}

[Serializable]
[XmlType("tTimerEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("timerEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record TimerEventDefinition : EventDefinition
{
    public TimerEventDefinition()
    {
        TimeDate = new Expression();
        TimeDuration = new Expression();
        TimeCycle = new Expression();
    }

    [XmlElement("timeDate", Order = 0)]
    public Expression TimeDate { get; set; } = new Expression();

    [XmlElement("timeDuration", Order = 1)]
    public Expression TimeDuration { get; set; } = new Expression();

    [XmlElement("timeCycle", Order = 2)]
    public Expression TimeCycle { get; set; } = new Expression();
}

[Serializable]
[XmlType("tTransaction", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("transaction", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Transaction : SubProcess
{
    public Transaction()
    {
        Method = "##Compensate";
    }

    public Transaction(string method) : this()
    {
        Method = method ?? "##Compensate";
    }

    [DefaultValue("##Compensate")]
    [XmlAttribute("method")]
    public string Method { get; set; } = "##Compensate";
}

[Serializable]
[XmlType("tUserTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("userTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record UserTask : Task
{
    public UserTask()
    {
        Renderings = new List<Rendering>();
        Implementation = "##unspecified";
    }

    [XmlElement("rendering", Order = 0)]
    public List<Rendering> Renderings { get; set; } = new List<Rendering>();

    [DefaultValue("##unspecified")]
    [XmlAttribute("implementation")]
    public string Implementation { get; set; } = "##unspecified";
}

[Serializable]
[XmlType("tDefinitions", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("definitions", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Definitions
{
    public Definitions()
    {
        Imports = new List<Import>();
        Extensions = new List<Extension>();
        RootElements = new List<RootElement>();
        BpmnDiagrams = new List<BpmnDiagram>();
        Relationships = new List<Relationship>();
        Id = string.Empty;
        Name = string.Empty;
        TargetNamespace = string.Empty;
        ExpressionLanguage = "http://www.w3.org/1999/XPath";
        TypeLanguage = "http://www.w3.org/2001/XMLSchema";
        Exporter = string.Empty;
        ExporterVersion = string.Empty;
        AnyAttributes = new List<XmlAttribute>();
    }

    [XmlElement("import", Order = 0)]
    public List<Import> Imports { get; set; } = new List<Import>();

    [XmlElement("extension", Order = 1)]
    public List<Extension> Extensions { get; set; } = new List<Extension>();

    [XmlElement("category", Type = typeof(Category), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("collaboration", Type = typeof(Collaboration), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("choreography", Type = typeof(Choreography), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalChoreographyTask", Type = typeof(GlobalChoreographyTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalConversation", Type = typeof(GlobalConversation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("correlationProperty", Type = typeof(CorrelationProperty), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("dataStore", Type = typeof(DataStore), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("endPoint", Type = typeof(EndPoint), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("error", Type = typeof(Error), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("escalation", Type = typeof(Escalation), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("eventDefinition", Type = typeof(EventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("cancelEventDefinition", Type = typeof(CancelEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("compensateEventDefinition", Type = typeof(CompensateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("conditionalEventDefinition", Type = typeof(ConditionalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("errorEventDefinition", Type = typeof(ErrorEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("escalationEventDefinition", Type = typeof(EscalationEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("linkEventDefinition", Type = typeof(LinkEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("messageEventDefinition", Type = typeof(MessageEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("signalEventDefinition", Type = typeof(SignalEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("terminateEventDefinition", Type = typeof(TerminateEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("timerEventDefinition", Type = typeof(TimerEventDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalBusinessRuleTask", Type = typeof(GlobalBusinessRuleTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalManualTask", Type = typeof(GlobalManualTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalScriptTask", Type = typeof(GlobalScriptTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalTask", Type = typeof(GlobalTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("globalUserTask", Type = typeof(GlobalUserTask), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("interface", Type = typeof(Interface), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("itemDefinition", Type = typeof(ItemDefinition), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("message", Type = typeof(Message), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("partnerEntity", Type = typeof(PartnerEntity), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("partnerRole", Type = typeof(PartnerRole), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("process", Type = typeof(Process), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("resource", Type = typeof(Resource), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("signal", Type = typeof(Signal), Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", Order = 2)]
    [XmlElement("rootElement", Order = 2)]
    public List<RootElement> RootElements { get; set; } = new List<RootElement>();

    [XmlElement("BPMNDiagram", Order = 3, Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
    public List<BpmnDiagram> BpmnDiagrams { get; set; } = new List<BpmnDiagram>();

    [XmlElement("relationship", Order = 4)]
    public List<Relationship> Relationships { get; set; } = new List<Relationship>();

    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("targetNamespace")]
    public string TargetNamespace { get; set; } = string.Empty;

    [DefaultValue("http://www.w3.org/1999/XPath")]
    [XmlAttribute("expressionLanguage")]
    public string ExpressionLanguage { get; set; } = "http://www.w3.org/1999/XPath";

    [DefaultValue("http://www.w3.org/2001/XMLSchema")]
    [XmlAttribute("typeLanguage")]
    public string TypeLanguage { get; set; } = "http://www.w3.org/2001/XMLSchema";

    [XmlAttribute("exporter")]
    public string Exporter { get; set; } = string.Empty;

    [XmlAttribute("exporterVersion")]
    public string ExporterVersion { get; set; } = string.Empty;

    [XmlAnyAttribute]
    public List<XmlAttribute> AnyAttributes { get; set; } = new List<XmlAttribute>();
}

[Serializable]
[XmlType("tImport", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[DebuggerStepThrough()]
[DesignerCategory("code")]
[XmlRoot("import", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public record Import
{
    public Import()
    {
        Namespace = string.Empty;
        Location = string.Empty;
        ImportType = string.Empty;
    }

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("location")]
    public string Location { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [XmlAttribute("importType")]
    public string ImportType { get; set; } = string.Empty;
}
