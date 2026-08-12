namespace VertexBPMN.Domain.Model.Dmn
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>
    /// <para>Color is a data type that represents a color value in the RGB format.</para>
    /// </summary>
    [Description("Color is a data type that represents a color value in the RGB format.")]
    [Serializable()]
    [XmlType("Color", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Color", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public record Color
    {
        public Color()
        {
        }

        /// <summary>
        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
        /// </summary>
        [Range(typeof(int), "0", "255")]
        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("red")]
        public int Red { get; set; } = 0;

        /// <summary>
        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
        /// </summary>
        [Range(typeof(int), "0", "255")]
        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("green")]
        public int Green { get; set; } = 0;

        /// <summary>
        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
        /// </summary>
        [Range(typeof(int), "0", "255")]
        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("blue")]
        public int Blue { get; set; } = 0;
    }

    /// <summary>
    /// <para>A Point specifies an location in some x-y coordinate system.</para>
    /// </summary>
    [Description("A Point specifies an location in some x-y coordinate system.")]
    [Serializable()]
    [XmlType("Point", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Point", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public record Point
    {
        public Point()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("x")]
        public double X { get; set; } = 0.0;

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("y")]
        public double Y { get; set; } = 0.0;
    }

    /// <summary>
    /// <para>Dimension specifies two lengths (width and height) along the x and y axes in some x-y coordinate system.</para>
    /// </summary>
    [Description(("Dimension specifies two lengths (width and height) along the x and y axes in some" +
        " x-y coordinate system."))]
    [Serializable()]
    [XmlType("Dimension", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Dimension", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public record Dimension
    {
        public Dimension()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("width")]
        public double Width { get; set; } = 0.0;

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("height")]
        public double Height { get; set; } = 0.0;
    }

    /// <summary>
    /// <para>Bounds specifies a rectangular area in some x-y coordinate system that is defined by a location (x and y) and a size (width and height).</para>
    /// </summary>
    [Description(("Bounds specifies a rectangular area in some x-y coordinate system that is defined" +
        " by a location (x and y) and a size (width and height)."))]
    [Serializable()]
    [XmlType("Bounds", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Bounds", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public record Bounds
    {
        public Bounds()
        {
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

    /// <summary>
    /// <para>AlignmentKind enumerates the possible options for alignment for layout purposes.</para>
    /// </summary>
    [Description("AlignmentKind enumerates the possible options for alignment for layout purposes.")]
    [Serializable()]
    [XmlType("AlignmentKind", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public enum AlignmentKind
    {

        [XmlEnum("start")]
        Start,

        [XmlEnum("end")]
        End,

        [XmlEnum("center")]
        Center,
    }

    /// <summary>
    /// <para>KnownColor is an enumeration of 17 known colors.</para>
    /// </summary>
    [Description("KnownColor is an enumeration of 17 known colors.")]
    [Serializable()]
    [XmlType("KnownColor", Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
    public enum KnownColor
    {

        /// <summary>
        /// <para>a color with a value of #800000</para>
        /// </summary>
        [Description("a color with a value of #800000")]
        [XmlEnum("maroon")]
        Maroon,

        /// <summary>
        /// <para>a color with a value of #FF0000</para>
        /// </summary>
        [Description("a color with a value of #FF0000")]
        [XmlEnum("red")]
        Red,

        /// <summary>
        /// <para>a color with a value of #FFA500</para>
        /// </summary>
        [Description("a color with a value of #FFA500")]
        [XmlEnum("orange")]
        Orange,

        /// <summary>
        /// <para>a color with a value of #FFFF00</para>
        /// </summary>
        [Description("a color with a value of #FFFF00")]
        [XmlEnum("yellow")]
        Yellow,

        /// <summary>
        /// <para>a color with a value of #808000</para>
        /// </summary>
        [Description("a color with a value of #808000")]
        [XmlEnum("olive")]
        Olive,

        /// <summary>
        /// <para>a color with a value of #800080</para>
        /// </summary>
        [Description("a color with a value of #800080")]
        [XmlEnum("purple")]
        Purple,

        /// <summary>
        /// <para>a color with a value of #FF00FF</para>
        /// </summary>
        [Description("a color with a value of #FF00FF")]
        [XmlEnum("fuchsia")]
        Fuchsia,

        /// <summary>
        /// <para>a color with a value of #FFFFFF</para>
        /// </summary>
        [Description("a color with a value of #FFFFFF")]
        [XmlEnum("white")]
        White,

        /// <summary>
        /// <para>a color with a value of #00FF00</para>
        /// </summary>
        [Description("a color with a value of #00FF00")]
        [XmlEnum("lime")]
        Lime,

        /// <summary>
        /// <para>a color with a value of #008000</para>
        /// </summary>
        [Description("a color with a value of #008000")]
        [XmlEnum("green")]
        Green,

        /// <summary>
        /// <para>a color with a value of #000080</para>
        /// </summary>
        [Description("a color with a value of #000080")]
        [XmlEnum("navy")]
        Navy,

        /// <summary>
        /// <para>a color with a value of #0000FF</para>
        /// </summary>
        [Description("a color with a value of #0000FF")]
        [XmlEnum("blue")]
        Blue,

        /// <summary>
        /// <para>a color with a value of #00FFFF</para>
        /// </summary>
        [Description("a color with a value of #00FFFF")]
        [XmlEnum("aqua")]
        Aqua,

        /// <summary>
        /// <para>a color with a value of #008080</para>
        /// </summary>
        [Description("a color with a value of #008080")]
        [XmlEnum("teal")]
        Teal,

        /// <summary>
        /// <para>a color with a value of #000000</para>
        /// </summary>
        [Description("a color with a value of #000000")]
        [XmlEnum("black")]
        Black,

        /// <summary>
        /// <para>a color with a value of #C0C0C0</para>
        /// </summary>
        [Description("a color with a value of #C0C0C0")]
        [XmlEnum("silver")]
        Silver,

        /// <summary>
        /// <para>a color with a value of #808080</para>
        /// </summary>
        [Description("a color with a value of #808080")]
        [XmlEnum("gray")]
        Gray,
    }

    /// <summary>
    /// <para>DiagramElement is the abstract super type of all elements in diagrams, including diagrams themselves. When contained in a diagram, diagram elements are laid out relative to the diagram's origin.</para>
    /// <para>This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence</para>
    /// </summary>
    [Description(@"DiagramElement is the abstract super type of all elements in diagrams, including diagrams themselves. When contained in a diagram, diagram elements are laid out relative to the diagram's origin. This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence")]
    [Serializable()]
    [XmlType("DiagramElement", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNDiagramElement", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [XmlInclude(typeof(Diagram))]
    [XmlInclude(typeof(DmnDecisionServiceDividerLine))]
    [XmlInclude(typeof(DmnDiagram))]
    [XmlInclude(typeof(DmnEdge))]
    [XmlInclude(typeof(DmnLabel))]
    [XmlInclude(typeof(DmnShape))]
    [XmlInclude(typeof(Edge))]
    [XmlInclude(typeof(Shape))]
    public abstract record DiagramElement
    {

        [XmlElement("extension", Order = 0)]
        public DiagramElementExtension Extension { get; set; } = new DiagramElementExtension();

        /// <summary>
        /// <para>an optional locally-owned style for this diagram element.</para>
        /// </summary>
        [Description("an optional locally-owned style for this diagram element.")]
        [XmlElement("DMNStyle", Type = typeof(DmnStyle), Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/", Order = 1)]
        [XmlElement("Style", Order = 1)]
        public Style Style { get; set; } // Abstrakt: kann nicht initialisiert werden

        /// <summary>
        /// <para>a reference to an optional shared style element for this diagram element.</para>
        /// </summary>
        [Description("a reference to an optional shared style element for this diagram element.")]
        [XmlAttribute("sharedStyle")]
        public string SharedStyle { get; set; } = string.Empty;

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore()]
        private Collection<XmlAttribute> _anyAttributes;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttributes
        {
            get
            {
                return _anyAttributes;
            }
            private set
            {
                _anyAttributes = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttributes collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnyAttributesSpecified
        {
            get
            {
                return (this.AnyAttributes.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="DiagramElement" /> class.</para>
        /// </summary>
        public DiagramElement()
        {
            this._anyAttributes = new Collection<XmlAttribute>();
        }
    }


    [Serializable()]
    [XmlType("DiagramElementExtension", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/", AnonymousType = true)]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DiagramElementExtension
    {

        [XmlIgnore()]
        private Collection<XmlElement> _any;

        [XmlAnyElement(Order = 0)]
        public Collection<XmlElement> Any
        {
            get
            {
                return _any;
            }
            private set
            {
                _any = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="DiagramElementExtension" /> class.</para>
        /// </summary>
        public DiagramElementExtension()
        {
            this._any = new Collection<XmlElement>();
        }
    }

    /// <summary>
    /// <para>Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves.</para>
    /// <para>This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence</para>
    /// </summary>
    [Description(@"Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves. This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence")]
    [Serializable()]
    [XmlType("Style", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Style", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [XmlInclude(typeof(DmnStyle))]
    public abstract record Style
    {

        [XmlElement("extension", Order = 0)]
        public StyleExtension Extension { get; set; } = new StyleExtension();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore()]
        private Collection<XmlAttribute> _anyAttributes;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttributes
        {
            get
            {
                return _anyAttributes;
            }
            private set
            {
                _anyAttributes = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttributes collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnyAttributesSpecified
        {
            get
            {
                return (this.AnyAttributes.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Style" /> class.</para>
        /// </summary>
        public Style()
        {
            this._anyAttributes = new Collection<XmlAttribute>();
        }
    }


    [Serializable()]
    [XmlType("StyleExtension", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/", AnonymousType = true)]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record StyleExtension
    {

        [XmlIgnore()]
        private Collection<XmlElement> _any;

        [XmlAnyElement(Order = 0)]
        public Collection<XmlElement> Any
        {
            get
            {
                return _any;
            }
            private set
            {
                _any = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="StyleExtension" /> class.</para>
        /// </summary>
        public StyleExtension()
        {
            this._any = new Collection<XmlElement>();
        }
    }


    [Serializable()]
    [XmlType("Diagram", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(DmnDiagram))]
    public abstract record Diagram : DiagramElement
    {
        public Diagram() : base()
        {
        }

        /// <summary>
        /// <para>the name of the diagram.</para>
        /// </summary>
        [Description("the name of the diagram.")]
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>the documentation of the diagram.</para>
        /// </summary>
        [Description("the documentation of the diagram.")]
        [XmlAttribute("documentation")]
        public string Documentation { get; set; } = string.Empty;

        /// <summary>
        /// <para>the resolution of the diagram expressed in user units per inch.</para>
        /// </summary>
        [Description("the resolution of the diagram expressed in user units per inch.")]
        [XmlAttribute("resolution")]
        public double ResolutionValue { get; set; } = 0.0;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Resolution property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ResolutionValueSpecified { get; set; } = false;

        /// <summary>
        /// <para>the resolution of the diagram expressed in user units per inch.</para>
        /// </summary>
        [XmlIgnore()]
        public System.Nullable<double> Resolution
        {
            get
            {
                if (this.ResolutionValueSpecified)
                {
                    return this.ResolutionValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.ResolutionValue = value.GetValueOrDefault();
                this.ResolutionValueSpecified = value.HasValue;
            }
        }
    }


    [Serializable()]
    [XmlType("Shape", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(DmnLabel))]
    [XmlInclude(typeof(DmnShape))]
    public abstract record Shape : DiagramElement
    {
        public Shape() : base()
        {
        }

        /// <summary>
        /// <para>the optional bounds of the shape relative to the origin of its nesting plane.</para>
        /// </summary>
        [Description("the optional bounds of the shape relative to the origin of its nesting plane.")]
        [XmlElement("Bounds", Order = 0, Namespace = "http://www.omg.org/spec/DMN/20180521/DC/")]
        public Bounds Bounds { get; set; } = new Bounds();
    }


    [Serializable()]
    [XmlType("Edge", Namespace = "http://www.omg.org/spec/DMN/20180521/DI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(DmnDecisionServiceDividerLine))]
    [XmlInclude(typeof(DmnEdge))]
    public abstract record Edge : DiagramElement
    {

        [XmlIgnore()]
        private Collection<Point> _waypoints;

        /// <summary>
        /// <para>an optional list of points relative to the origin of the nesting diagram that specifies the connected line segments of the edge</para>
        /// </summary>
        [Description(("an optional list of points relative to the origin of the nesting diagram that spe" +
            "cifies the connected line segments of the edge"))]
        [XmlElement("waypoint", Order = 0)]
        public Collection<Point> Waypoints
        {
            get
            {
                return _waypoints;
            }
            private set
            {
                _waypoints = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Waypoints collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool WaypointsSpecified
        {
            get
            {
                return (this.Waypoints.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Edge" /> class.</para>
        /// </summary>
        public Edge() : base()
        {
            this._waypoints = new Collection<Point>();
        }
    }


    [Serializable()]
    [XmlType("DMNDI", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNDI", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record Dmndi
    {

        [XmlIgnore()]
        private Collection<DmnDiagram> _dmnDiagrams;

        [XmlElement("DMNDiagram", Order = 0)]
        public Collection<DmnDiagram> DmnDiagrams
        {
            get
            {
                return _dmnDiagrams;
            }
            private set
            {
                _dmnDiagrams = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DmnDiagrams collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DmnDiagramsSpecified
        {
            get
            {
                return (this.DmnDiagrams.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Dmndi" /> class.</para>
        /// </summary>
        public Dmndi()
        {
            this._dmnDiagrams = new Collection<DmnDiagram>();
            this._dmnStyles = new Collection<DmnStyle>();
        }

        [XmlIgnore()]
        private Collection<DmnStyle> _dmnStyles;

        [XmlElement("DMNStyle", Order = 1)]
        public Collection<DmnStyle> DmnStyles
        {
            get
            {
                return _dmnStyles;
            }
            private set
            {
                _dmnStyles = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DmnStyles collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DmnStylesSpecified
        {
            get
            {
                return (this.DmnStyles.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("DMNDiagram", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNDiagram", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnDiagram : Diagram
    {

        [XmlElement("Size", Order = 0)]
        public Dimension Size { get; set; } = new Dimension();

        [XmlIgnore()]
        private Collection<DiagramElement> _dmnDiagramElements;

        [XmlElement("DMNShape", Type = typeof(DmnShape), Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/", Order = 1)]
        [XmlElement("DMNEdge", Type = typeof(DmnEdge), Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/", Order = 1)]
        [XmlElement("DMNDiagramElement", Order = 1)]
        public Collection<DiagramElement> DmnDiagramElements
        {
            get
            {
                return _dmnDiagramElements;
            }
            private set
            {
                _dmnDiagramElements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DmnDiagramElements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DmnDiagramElementsSpecified
        {
            get
            {
                return (this.DmnDiagramElements.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="DmnDiagram" /> class.</para>
        /// </summary>
        public DmnDiagram() : base()
        {
            this._dmnDiagramElements = new Collection<DiagramElement>();
        }

        [XmlIgnore()]
        private bool _useAlternativeInputDataShape = false;

        [DefaultValue(false)]
        [XmlAttribute("useAlternativeInputDataShape")]
        public bool UseAlternativeInputDataShape
        {
            get
            {
                return _useAlternativeInputDataShape;
            }
            set
            {
                _useAlternativeInputDataShape = value;
            }
        }
    }


    [Serializable()]
    [XmlType("DMNStyle", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNStyle", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnStyle : Style
    {
        public DmnStyle() : base()
        {
        }

        [XmlElement("FillColor", Order = 0)]
        public Color FillColor { get; set; } = new Color();

        [XmlElement("StrokeColor", Order = 1)]
        public Color StrokeColor { get; set; } = new Color();

        [XmlElement("FontColor", Order = 2)]
        public Color FontColor { get; set; } = new Color();

        [XmlAttribute("fontFamily")]
        public string FontFamily { get; set; } = string.Empty;


        [XmlAttribute("fontSize")]
        public double FontSizeValue { get; set; } = 0.0;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the FontSize property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FontSizeValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<double> FontSize
        {
            get
            {
                if (this.FontSizeValueSpecified)
                {
                    return this.FontSizeValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.FontSizeValue = value.GetValueOrDefault();
                this.FontSizeValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("fontItalic")]
        public bool FontItalicValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the FontItalic property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FontItalicValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<bool> FontItalic
        {
            get
            {
                if (this.FontItalicValueSpecified)
                {
                    return this.FontItalicValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.FontItalicValue = value.GetValueOrDefault();
                this.FontItalicValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("fontBold")]
        public bool FontBoldValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the FontBold property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FontBoldValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<bool> FontBold
        {
            get
            {
                if (this.FontBoldValueSpecified)
                {
                    return this.FontBoldValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.FontBoldValue = value.GetValueOrDefault();
                this.FontBoldValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("fontUnderline")]
        public bool FontUnderlineValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the FontUnderline property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FontUnderlineValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<bool> FontUnderline
        {
            get
            {
                if (this.FontUnderlineValueSpecified)
                {
                    return this.FontUnderlineValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.FontUnderlineValue = value.GetValueOrDefault();
                this.FontUnderlineValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("fontStrikeThrough")]
        public bool FontStrikeThroughValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the FontStrikeThrough property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FontStrikeThroughValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<bool> FontStrikeThrough
        {
            get
            {
                if (this.FontStrikeThroughValueSpecified)
                {
                    return this.FontStrikeThroughValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.FontStrikeThroughValue = value.GetValueOrDefault();
                this.FontStrikeThroughValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("labelHorizontalAlignement")]
        public AlignmentKind LabelHorizontalAlignementValue { get; set; }

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelHorizontalAlignement property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool LabelHorizontalAlignementValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<AlignmentKind> LabelHorizontalAlignement
        {
            get
            {
                if (this.LabelHorizontalAlignementValueSpecified)
                {
                    return this.LabelHorizontalAlignementValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.LabelHorizontalAlignementValue = value.GetValueOrDefault();
                this.LabelHorizontalAlignementValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("labelVerticalAlignment")]
        public AlignmentKind LabelVerticalAlignmentValue { get; set; }

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelVerticalAlignment property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool LabelVerticalAlignmentValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<AlignmentKind> LabelVerticalAlignment
        {
            get
            {
                if (this.LabelVerticalAlignmentValueSpecified)
                {
                    return this.LabelVerticalAlignmentValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.LabelVerticalAlignmentValue = value.GetValueOrDefault();
                this.LabelVerticalAlignmentValueSpecified = value.HasValue;
            }
        }
    }


    [Serializable()]
    [XmlType("DMNShape", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNShape", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnShape : Shape
    {
        public DmnShape() : base()
        {
        }

        [XmlElement("DMNLabel", Order = 0)]
        public DmnLabel DmnLabel { get; set; } = new DmnLabel();

        [XmlElement("DMNDecisionServiceDividerLine", Order = 1)]
        public DmnDecisionServiceDividerLine DmnDecisionServiceDividerLine { get; set; } = new DmnDecisionServiceDividerLine();

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("dmnElementRef")]
        public System.Xml.XmlQualifiedName DmnElementRef { get; set; } = System.Xml.XmlQualifiedName.Empty;


        [XmlAttribute("isListedInputData")]
        public bool IsListedInputDataValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsListedInputData property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool IsListedInputDataValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<bool> IsListedInputData
        {
            get
            {
                if (this.IsListedInputDataValueSpecified)
                {
                    return this.IsListedInputDataValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.IsListedInputDataValue = value.GetValueOrDefault();
                this.IsListedInputDataValueSpecified = value.HasValue;
            }
        }

        [XmlIgnore()]
        private bool _isCollapsed = false;

        [DefaultValue(false)]
        [XmlAttribute("isCollapsed")]
        public bool IsCollapsed
        {
            get
            {
                return _isCollapsed;
            }
            set
            {
                _isCollapsed = value;
            }
        }
    }


    [Serializable()]
    [XmlType("DMNLabel", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNLabel", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnLabel : Shape
    {
        public DmnLabel() : base()
        {
        }

        [XmlElement("Text", Order = 0)]
        public string Text { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("DMNDecisionServiceDividerLine", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNDecisionServiceDividerLine", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnDecisionServiceDividerLine : Edge
    {
        public DmnDecisionServiceDividerLine() : base()
        {
        }
    }


    [Serializable()]
    [XmlType("DMNEdge", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNEdge", Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
    public record DmnEdge : Edge
    {
        public DmnEdge() : base()
        {
        }

        [XmlElement("DMNLabel", Order = 0)]
        public DmnLabel DmnLabel { get; set; } = new DmnLabel();

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("dmnElementRef")]
        public System.Xml.XmlQualifiedName DmnElementRef { get; set; } = System.Xml.XmlQualifiedName.Empty;

        [XmlAttribute("sourceElement")]
        public System.Xml.XmlQualifiedName SourceElement { get; set; } = System.Xml.XmlQualifiedName.Empty;

        [XmlAttribute("targetElement")]
        public System.Xml.XmlQualifiedName TargetElement { get; set; } = System.Xml.XmlQualifiedName.Empty;
    }


    [Serializable()]
    [XmlType("tDMNElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DMNElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(Every))]
    [XmlInclude(typeof(Some))]
    [XmlInclude(typeof(DmnArtifact))]
    [XmlInclude(typeof(DmnAssociation))]
    [XmlInclude(typeof(DmnAuthorityRequirement))]
    [XmlInclude(typeof(DmnBusinessContextElement))]
    [XmlInclude(typeof(DmnBusinessKnowledgeModel))]
    [XmlInclude(typeof(DmnConditional))]
    [XmlInclude(typeof(DmnContext))]
    [XmlInclude(typeof(DmnContextEntry))]
    [XmlInclude(typeof(DmnDecision))]
    [XmlInclude(typeof(DmnDecisionRule))]
    [XmlInclude(typeof(DmnDecisionService))]
    [XmlInclude(typeof(DmnDecisionTable))]
    [XmlInclude(typeof(DmnDefinitions))]
    [XmlInclude(typeof(DmnDRGElement))]
    [XmlInclude(typeof(DmnElementCollection))]
    [XmlInclude(typeof(DmnExpression))]
    [XmlInclude(typeof(DmnFilter))]
    [XmlInclude(typeof(DmnFor))]
    [XmlInclude(typeof(DmnFunctionDefinition))]
    [XmlInclude(typeof(DmnFunctionItem))]
    [XmlInclude(typeof(DmnGroup))]
    [XmlInclude(typeof(DmnImport))]
    [XmlInclude(typeof(DmnImportedValues))]
    [XmlInclude(typeof(DmnInformationItem))]
    [XmlInclude(typeof(DmnInformationRequirement))]
    [XmlInclude(typeof(DmnInputClause))]
    [XmlInclude(typeof(DmnInputData))]
    [XmlInclude(typeof(DmnInvocable))]
    [XmlInclude(typeof(DmnInvocation))]
    [XmlInclude(typeof(DmnItemDefinition))]
    [XmlInclude(typeof(DmnIterator))]
    [XmlInclude(typeof(DmnKnowledgeRequirement))]
    [XmlInclude(typeof(DmnKnowledgeSource))]
    [XmlInclude(typeof(DmnList))]
    [XmlInclude(typeof(DmnLiteralExpression))]
    [XmlInclude(typeof(DmnNamedElement))]
    [XmlInclude(typeof(DmnOrganizationUnit))]
    [XmlInclude(typeof(DmnOutputClause))]
    [XmlInclude(typeof(DmnPerformanceIndicator))]
    [XmlInclude(typeof(DmnQuantified))]
    [XmlInclude(typeof(DmnRelation))]
    [XmlInclude(typeof(DmnTextAnnotation))]
    [XmlInclude(typeof(DmnUnaryTests))]
    public record DmnElement
    {

        [XmlElement("description", Order = 0)]
        public string Description { get; set; } = string.Empty;

        [XmlElement("extensionElements", Order = 1)]
        public DmnElementExtensionElements ExtensionElements { get; set; } = new DmnElementExtensionElements();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("label")]
        public string Label { get; set; } = string.Empty;

        [XmlIgnore()]
        private Collection<XmlAttribute> _anyAttributes;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttributes
        {
            get
            {
                return _anyAttributes;
            }
            private set
            {
                _anyAttributes = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttributes collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnyAttributesSpecified
        {
            get
            {
                return (this.AnyAttributes.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TdmnElement" /> class.</para>
        /// </summary>
        public DmnElement()
        {
            this._anyAttributes = new Collection<XmlAttribute>();
        }
    }


    [Serializable()]
    [XmlType("TdmnElementExtensionElements", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", AnonymousType = true)]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnElementExtensionElements
    {

        [XmlIgnore()]
        private Collection<XmlElement> _any;

        [XmlAnyElement(Order = 0)]
        public Collection<XmlElement> Any
        {
            get
            {
                return _any;
            }
            private set
            {
                _any = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TdmnElementExtensionElements" /> class.</para>
        /// </summary>
        public DmnElementExtensionElements()
        {
            this._any = new Collection<XmlElement>();
        }
    }


    [Serializable()]
    [XmlType("tNamedElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("namedElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnBusinessContextElement))]
    [XmlInclude(typeof(DmnBusinessKnowledgeModel))]
    [XmlInclude(typeof(DmnDecision))]
    [XmlInclude(typeof(DmnDecisionService))]
    [XmlInclude(typeof(DmnDefinitions))]
    [XmlInclude(typeof(DmnDRGElement))]
    [XmlInclude(typeof(DmnElementCollection))]
    [XmlInclude(typeof(DmnImport))]
    [XmlInclude(typeof(DmnImportedValues))]
    [XmlInclude(typeof(DmnInformationItem))]
    [XmlInclude(typeof(DmnInputData))]
    [XmlInclude(typeof(DmnInvocable))]
    [XmlInclude(typeof(DmnItemDefinition))]
    [XmlInclude(typeof(DmnKnowledgeSource))]
    [XmlInclude(typeof(DmnOrganizationUnit))]
    [XmlInclude(typeof(DmnPerformanceIndicator))]
    public record DmnNamedElement : DmnElement
    {
        public DmnNamedElement() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tDMNElementReference", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnElementReference
    {
        public DmnElementReference()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("href")]
        public string Href { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tDefinitions", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("definitions", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnDefinitions : DmnNamedElement
    {

        [XmlIgnore()]
        private Collection<DmnImport> _imports;

        [XmlElement("import", Order = 0)]
        public Collection<DmnImport> Imports
        {
            get
            {
                return _imports;
            }
            private set
            {
                _imports = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Imports collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ImportsSpecified
        {
            get
            {
                return (this.Imports.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDefinitions" /> class.</para>
        /// </summary>
        public DmnDefinitions() : base()
        {
            this._imports = new Collection<DmnImport>();
            this._itemDefinitions = new Collection<DmnItemDefinition>();
            this._drgElements = new Collection<DmnDRGElement>();
            this._artifacts = new Collection<DmnArtifact>();
            this._elementCollections = new Collection<DmnElementCollection>();
            this._businessContextElements = new Collection<DmnBusinessContextElement>();
        }

        [XmlIgnore()]
        private Collection<DmnItemDefinition> _itemDefinitions;

        [XmlElement("itemDefinition", Order = 1)]
        public Collection<DmnItemDefinition> ItemDefinitions
        {
            get
            {
                return _itemDefinitions;
            }
            private set
            {
                _itemDefinitions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ItemDefinitions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ItemDefinitionsSpecified
        {
            get
            {
                return (this.ItemDefinitions.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnDRGElement> _drgElements;

        [XmlElement("decision", Type = typeof(DmnDecision), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("invocable", Type = typeof(DmnInvocable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("businessKnowledgeModel", Type = typeof(DmnBusinessKnowledgeModel), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("decisionService", Type = typeof(DmnDecisionService), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("inputData", Type = typeof(DmnInputData), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("knowledgeSource", Type = typeof(DmnKnowledgeSource), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 2)]
        [XmlElement("drgElement", Order = 2)]
        public Collection<DmnDRGElement> DrgElements
        {
            get
            {
                return _drgElements;
            }
            private set
            {
                _drgElements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DrgElements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DrgElementsSpecified
        {
            get
            {
                return (this.DrgElements.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnArtifact> _artifacts;

        [XmlElement("group", Type = typeof(DmnGroup), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 3)]
        [XmlElement("textAnnotation", Type = typeof(DmnTextAnnotation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 3)]
        [XmlElement("association", Type = typeof(DmnAssociation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 3)]
        [XmlElement("artifact", Order = 3)]
        public Collection<DmnArtifact> Artifacts
        {
            get
            {
                return _artifacts;
            }
            private set
            {
                _artifacts = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Artifacts collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ArtifactsSpecified
        {
            get
            {
                return (this.Artifacts.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementCollection> _elementCollections;

        [XmlElement("elementCollection", Order = 4)]
        public Collection<DmnElementCollection> ElementCollections
        {
            get
            {
                return _elementCollections;
            }
            private set
            {
                _elementCollections = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ElementCollections collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ElementCollectionsSpecified
        {
            get
            {
                return (this.ElementCollections.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnBusinessContextElement> _businessContextElements;

        [XmlElement("performanceIndicator", Type = typeof(DmnPerformanceIndicator), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 5)]
        [XmlElement("organizationUnit", Type = typeof(DmnOrganizationUnit), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 5)]
        [XmlElement("businessContextElement", Order = 5)]
        public Collection<DmnBusinessContextElement> BusinessContextElements
        {
            get
            {
                return _businessContextElements;
            }
            private set
            {
                _businessContextElements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the BusinessContextElements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool BusinessContextElementsSpecified
        {
            get
            {
                return (this.BusinessContextElements.Count != 0);
            }
        }

        [XmlElement("DMNDI", Order = 6, Namespace = "https://www.omg.org/spec/DMN/20230324/DMNDI/")]
        public Dmndi Dmndi { get; set; } = new Dmndi();

        [XmlIgnore()]
        private string _expressionLanguage = "https://www.omg.org/spec/DMN/20240513/FEEL/";

        [DefaultValue("https://www.omg.org/spec/DMN/20240513/FEEL/")]
        [XmlAttribute("expressionLanguage")]
        public string ExpressionLanguage
        {
            get
            {
                return _expressionLanguage;
            }
            set
            {
                _expressionLanguage = value;
            }
        }

        [XmlIgnore()]
        private string _typeLanguage = "https://www.omg.org/spec/DMN/20240513/FEEL/";

        [DefaultValue("https://www.omg.org/spec/DMN/20240513/FEEL/")]
        [XmlAttribute("typeLanguage")]
        public string TypeLanguage
        {
            get
            {
                return _typeLanguage;
            }
            set
            {
                _typeLanguage = value;
            }
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [XmlAttribute("exporter")]
        public string Exporter { get; set; } = string.Empty;

        [XmlAttribute("exporterVersion")]
        public string ExporterVersion { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tImport", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("import", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnImportedValues))]
    public record DmnImport : DmnNamedElement
    {
        public DmnImport() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [XmlAttribute("locationURI")]
        public string LocationUri { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("importType")]
        public string ImportType { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tItemDefinition", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("itemDefinition", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnItemDefinition : DmnNamedElement
    {

        [XmlElement("typeRef", Order = 0)]
        public string TypeRef { get; set; } = string.Empty;

        [XmlElement("allowedValues", Order = 1)]
        public DmnUnaryTests AllowedValues { get; set; } = new DmnUnaryTests();

        [XmlElement("typeConstraint", Order = 2)]
        public DmnUnaryTests TypeConstraint { get; set; } = new DmnUnaryTests();

        [XmlIgnore()]
        private Collection<DmnItemDefinition> _itemComponents;

        [XmlElement("itemComponent", Order = 3)]
        public Collection<DmnItemDefinition> ItemComponents
        {
            get
            {
                return _itemComponents;
            }
            private set
            {
                _itemComponents = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ItemComponents collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ItemComponentsSpecified
        {
            get
            {
                return (this.ItemComponents.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TItemDefinition" /> class.</para>
        /// </summary>
        public DmnItemDefinition() : base()
        {
            this._itemComponents = new Collection<DmnItemDefinition>();
        }

        [XmlElement("functionItem", Order = 4)]
        public DmnFunctionItem FunctionItem { get; set; } = new DmnFunctionItem();

        [XmlAttribute("typeLanguage")]
        public string TypeLanguage { get; set; } = string.Empty;

        [XmlIgnore()]
        private bool _isCollection = false;

        [DefaultValue(false)]
        [XmlAttribute("isCollection")]
        public bool IsCollection
        {
            get
            {
                return _isCollection;
            }
            set
            {
                _isCollection = value;
            }
        }
    }


    [Serializable()]
    [XmlType("tUnaryTests", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnUnaryTests : DmnExpression
    {
        public DmnUnaryTests() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("text", Order = 0)]
        public string Text { get; set; } = string.Empty;

        [XmlAttribute("expressionLanguage")]
        public string ExpressionLanguage { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tExpression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("expression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(Every))]
    [XmlInclude(typeof(Some))]
    [XmlInclude(typeof(DmnConditional))]
    [XmlInclude(typeof(DmnContext))]
    [XmlInclude(typeof(DmnDecisionTable))]
    [XmlInclude(typeof(DmnFilter))]
    [XmlInclude(typeof(DmnFor))]
    [XmlInclude(typeof(DmnFunctionDefinition))]
    [XmlInclude(typeof(DmnInvocation))]
    [XmlInclude(typeof(DmnIterator))]
    [XmlInclude(typeof(DmnList))]
    [XmlInclude(typeof(DmnLiteralExpression))]
    [XmlInclude(typeof(DmnQuantified))]
    [XmlInclude(typeof(DmnRelation))]
    [XmlInclude(typeof(DmnUnaryTests))]
    public record DmnExpression : DmnElement
    {
        public DmnExpression() : base()
        {
        }

        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tFunctionItem", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("functionItem", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnFunctionItem : DmnElement
    {

        [XmlIgnore()]
        private Collection<DmnInformationItem> _parameters;

        [XmlElement("parameters", Order = 0)]
        public Collection<DmnInformationItem> Parameters
        {
            get
            {
                return _parameters;
            }
            private set
            {
                _parameters = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Parameters collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ParametersSpecified
        {
            get
            {
                return (this.Parameters.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TFunctionItem" /> class.</para>
        /// </summary>
        public DmnFunctionItem() : base()
        {
            this._parameters = new Collection<DmnInformationItem>();
        }

        [XmlAttribute("outputTypeRef")]
        public string OutputTypeRef { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tInformationItem", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("informationItem", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnInformationItem : DmnNamedElement
    {
        public DmnInformationItem() : base()
        {
        }

        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tDRGElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("drgElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnBusinessKnowledgeModel))]
    [XmlInclude(typeof(DmnDecision))]
    [XmlInclude(typeof(DmnDecisionService))]
    [XmlInclude(typeof(DmnInputData))]
    [XmlInclude(typeof(DmnInvocable))]
    [XmlInclude(typeof(DmnKnowledgeSource))]
    public record DmnDRGElement : DmnNamedElement
    {
        public DmnDRGElement() : base()
        {
        }
    }


    [Serializable()]
    [XmlType("tArtifact", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("artifact", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnAssociation))]
    [XmlInclude(typeof(DmnGroup))]
    [XmlInclude(typeof(DmnTextAnnotation))]
    public record DmnArtifact : DmnElement
    {
        public DmnArtifact() : base()
        {
        }
    }


    [Serializable()]
    [XmlType("tElementCollection", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("elementCollection", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnElementCollection : DmnNamedElement
    {

        [XmlIgnore()]
        private Collection<DmnElementReference> _drgElements;

        [XmlElement("decision", Type = typeof(DmnDecision), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("invocable", Type = typeof(DmnInvocable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("businessKnowledgeModel", Type = typeof(DmnBusinessKnowledgeModel), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("decisionService", Type = typeof(DmnDecisionService), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("inputData", Type = typeof(DmnInputData), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("knowledgeSource", Type = typeof(DmnKnowledgeSource), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("drgElement", Order = 0)]
        public Collection<DmnElementReference> DrgElements
        {
            get
            {
                return _drgElements;
            }
            private set
            {
                _drgElements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DrgElements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DrgElementsSpecified
        {
            get
            {
                return (this.DrgElements.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TElementCollection" /> class.</para>
        /// </summary>
        public DmnElementCollection() : base()
        {
            this._drgElements = new Collection<DmnElementReference>();
        }
    }


    [Serializable()]
    [XmlType("tBusinessContextElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("businessContextElement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnOrganizationUnit))]
    [XmlInclude(typeof(DmnPerformanceIndicator))]
    public record DmnBusinessContextElement : DmnNamedElement
    {
        public DmnBusinessContextElement() : base()
        {
        }

        [XmlAttribute("URI")]
        public string Uri { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tDecision", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("decision", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnDecision : DmnDRGElement
    {

        [XmlElement("question", Order = 0)]
        public string Question { get; set; } = string.Empty;

        [XmlElement("allowedAnswers", Order = 1)]
        public string AllowedAnswers { get; set; } = string.Empty;

        [XmlElement("variable", Order = 2)]
        public DmnInformationItem Variable { get; set; } = new DmnInformationItem();

        [XmlIgnore()]
        private Collection<DmnInformationRequirement> _informationRequirements;

        [XmlElement("informationRequirement", Order = 3)]
        public Collection<DmnInformationRequirement> InformationRequirements
        {
            get
            {
                return _informationRequirements;
            }
            private set
            {
                _informationRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InformationRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool InformationRequirementsSpecified
        {
            get
            {
                return (this.InformationRequirements.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecision" /> class.</para>
        /// </summary>
        public DmnDecision() : base()
        {
            this._informationRequirements = new Collection<DmnInformationRequirement>();
            this._knowledgeRequirements = new Collection<DmnKnowledgeRequirement>();
            this._authorityRequirements = new Collection<DmnAuthorityRequirement>();
            this._supportedObjectives = new Collection<DmnElementReference>();
            this._impactedPerformanceIndicators = new Collection<DmnElementReference>();
            this._decisionMakers = new Collection<DmnElementReference>();
            this._decisionOwners = new Collection<DmnElementReference>();
            this._usingProcesses = new Collection<DmnElementReference>();
            this._usingTasks = new Collection<DmnElementReference>();
        }

        [XmlIgnore()]
        private Collection<DmnKnowledgeRequirement> _knowledgeRequirements;

        [XmlElement("knowledgeRequirement", Order = 4)]
        public Collection<DmnKnowledgeRequirement> KnowledgeRequirements
        {
            get
            {
                return _knowledgeRequirements;
            }
            private set
            {
                _knowledgeRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the KnowledgeRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool KnowledgeRequirementsSpecified
        {
            get
            {
                return (this.KnowledgeRequirements.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnAuthorityRequirement> _authorityRequirements;

        [XmlElement("authorityRequirement", Order = 5)]
        public Collection<DmnAuthorityRequirement> AuthorityRequirements
        {
            get
            {
                return _authorityRequirements;
            }
            private set
            {
                _authorityRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AuthorityRequirementsSpecified
        {
            get
            {
                return (this.AuthorityRequirements.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _supportedObjectives;

        [XmlElement("supportedObjective", Order = 6)]
        public Collection<DmnElementReference> SupportedObjectives
        {
            get
            {
                return _supportedObjectives;
            }
            private set
            {
                _supportedObjectives = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the SupportedObjectives collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool SupportedObjectivesSpecified
        {
            get
            {
                return (this.SupportedObjectives.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _impactedPerformanceIndicators;

        [XmlElement("impactedPerformanceIndicator", Order = 7)]
        public Collection<DmnElementReference> ImpactedPerformanceIndicators
        {
            get
            {
                return _impactedPerformanceIndicators;
            }
            private set
            {
                _impactedPerformanceIndicators = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ImpactedPerformanceIndicators collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ImpactedPerformanceIndicatorsSpecified
        {
            get
            {
                return (this.ImpactedPerformanceIndicators.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _decisionMakers;

        [XmlElement("decisionMaker", Order = 8)]
        public Collection<DmnElementReference> DecisionMakers
        {
            get
            {
                return _decisionMakers;
            }
            private set
            {
                _decisionMakers = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DecisionMakers collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DecisionMakersSpecified
        {
            get
            {
                return (this.DecisionMakers.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _decisionOwners;

        [XmlElement("decisionOwner", Order = 9)]
        public Collection<DmnElementReference> DecisionOwners
        {
            get
            {
                return _decisionOwners;
            }
            private set
            {
                _decisionOwners = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DecisionOwners collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DecisionOwnersSpecified
        {
            get
            {
                return (this.DecisionOwners.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _usingProcesses;

        [XmlElement("usingProcess", Order = 10)]
        public Collection<DmnElementReference> UsingProcesses
        {
            get
            {
                return _usingProcesses;
            }
            private set
            {
                _usingProcesses = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the UsingProcesses collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool UsingProcessesSpecified
        {
            get
            {
                return (this.UsingProcesses.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _usingTasks;

        [XmlElement("usingTask", Order = 11)]
        public Collection<DmnElementReference> UsingTasks
        {
            get
            {
                return _usingTasks;
            }
            private set
            {
                _usingTasks = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the UsingTasks collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool UsingTasksSpecified
        {
            get
            {
                return (this.UsingTasks.Count != 0);
            }
        }

        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 12)]
        [XmlElement("expression", Order = 12)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden
    }


    [Serializable()]
    [XmlType("tInformationRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("informationRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnInformationRequirement : DmnElement
    {
        public DmnInformationRequirement() : base()
        {
        }

        [XmlElement("requiredDecision", Order = 0)]
        public DmnElementReference RequiredDecision { get; set; } = new DmnElementReference();

        [XmlElement("requiredInput", Order = 1)]
        public DmnElementReference RequiredInput { get; set; } = new DmnElementReference();
    }


    [Serializable()]
    [XmlType("tKnowledgeRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("knowledgeRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnKnowledgeRequirement : DmnElement
    {
        public DmnKnowledgeRequirement() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("requiredKnowledge", Order = 0)]
        public DmnElementReference RequiredKnowledge { get; set; } = new DmnElementReference();
    }


    [Serializable()]
    [XmlType("tAuthorityRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("authorityRequirement", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnAuthorityRequirement : DmnElement
    {
        public DmnAuthorityRequirement() : base()
        {
        }

        [XmlElement("requiredDecision", Order = 0)]
        public DmnElementReference RequiredDecision { get; set; } = new DmnElementReference();

        [XmlElement("requiredInput", Order = 1)]
        public DmnElementReference RequiredInput { get; set; } = new DmnElementReference();

        [XmlElement("requiredAuthority", Order = 2)]
        public DmnElementReference RequiredAuthority { get; set; } = new DmnElementReference();
    }


    [Serializable()]
    [XmlType("tPerformanceIndicator", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("performanceIndicator", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnPerformanceIndicator : DmnBusinessContextElement
    {

        [XmlIgnore()]
        private Collection<DmnElementReference> _impactingDecisions;

        [XmlElement("impactingDecision", Order = 0)]
        public Collection<DmnElementReference> ImpactingDecisions
        {
            get
            {
                return _impactingDecisions;
            }
            private set
            {
                _impactingDecisions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ImpactingDecisions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ImpactingDecisionsSpecified
        {
            get
            {
                return (this.ImpactingDecisions.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TPerformanceIndicator" /> class.</para>
        /// </summary>
        public DmnPerformanceIndicator() : base()
        {
            this._impactingDecisions = new Collection<DmnElementReference>();
        }
    }

    [Serializable()]
    [XmlType("tOrganizationUnit", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("organizationUnit", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnOrganizationUnit : DmnBusinessContextElement
    {

        [XmlIgnore()]
        private Collection<DmnElementReference> _decisionsMade;

        [XmlElement("decisionMade", Order = 0)]
        public Collection<DmnElementReference> DecisionsMade
        {
            get
            {
                return _decisionsMade;
            }
            private set
            {
                _decisionsMade = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DecisionsMade collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DecisionsMadeSpecified
        {
            get
            {
                return (this.DecisionsMade.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TOrganizationUnit" /> class.</para>
        /// </summary>
        public DmnOrganizationUnit() : base()
        {
            this._decisionsMade = new Collection<DmnElementReference>();
            this._decisionsOwned = new Collection<DmnElementReference>();
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _decisionsOwned;

        [XmlElement("decisionOwned", Order = 1)]
        public Collection<DmnElementReference> DecisionsOwned
        {
            get
            {
                return _decisionsOwned;
            }
            private set
            {
                _decisionsOwned = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DecisionsOwned collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool DecisionsOwnedSpecified
        {
            get
            {
                return (this.DecisionsOwned.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("tInvocable", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("invocable", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [XmlInclude(typeof(DmnBusinessKnowledgeModel))]
    [XmlInclude(typeof(DmnDecisionService))]
    public record DmnInvocable : DmnDRGElement
    {
        public DmnInvocable() : base()
        {
        }

        [XmlElement("variable", Order = 0)]
        public DmnInformationItem Variable { get; set; } = new DmnInformationItem();
    }


    [Serializable()]
    [XmlType("tBusinessKnowledgeModel", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("businessKnowledgeModel", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnBusinessKnowledgeModel : DmnInvocable
    {

        [XmlElement("encapsulatedLogic", Order = 0)]
        public DmnFunctionDefinition EncapsulatedLogic { get; set; } = new DmnFunctionDefinition();

        [XmlIgnore()]
        private Collection<DmnKnowledgeRequirement> _knowledgeRequirements;

        [XmlElement("knowledgeRequirement", Order = 1)]
        public Collection<DmnKnowledgeRequirement> KnowledgeRequirements
        {
            get
            {
                return _knowledgeRequirements;
            }
            private set
            {
                _knowledgeRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the KnowledgeRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool KnowledgeRequirementsSpecified
        {
            get
            {
                return (this.KnowledgeRequirements.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TBusinessKnowledgeModel" /> class.</para>
        /// </summary>
        public DmnBusinessKnowledgeModel() : base()
        {
            this._knowledgeRequirements = new Collection<DmnKnowledgeRequirement>();
            this._authorityRequirements = new Collection<DmnAuthorityRequirement>();
        }

        [XmlIgnore()]
        private Collection<DmnAuthorityRequirement> _authorityRequirements;

        [XmlElement("authorityRequirement", Order = 2)]
        public Collection<DmnAuthorityRequirement> AuthorityRequirements
        {
            get
            {
                return _authorityRequirements;
            }
            private set
            {
                _authorityRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AuthorityRequirementsSpecified
        {
            get
            {
                return (this.AuthorityRequirements.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("tFunctionDefinition", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("functionDefinition", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnFunctionDefinition : DmnExpression
    {

        [XmlIgnore()]
        private Collection<DmnInformationItem> _formalParameters;

        [XmlElement("formalParameter", Order = 0)]
        public Collection<DmnInformationItem> FormalParameters
        {
            get
            {
                return _formalParameters;
            }
            private set
            {
                _formalParameters = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FormalParameters collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool FormalParametersSpecified
        {
            get
            {
                return (this.FormalParameters.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TFunctionDefinition" /> class.</para>
        /// </summary>
        public DmnFunctionDefinition() : base()
        {
            this._formalParameters = new Collection<DmnInformationItem>();
        }

        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("expression", Order = 1)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden

        [XmlIgnore()]
        private DmnFunctionKind _kind = DmnFunctionKind.Feel;

        [DefaultValue(DmnFunctionKind.Feel)]
        [XmlAttribute("kind")]
        public DmnFunctionKind Kind
        {
            get
            {
                return _kind;
            }
            set
            {
                _kind = value;
            }
        }
    }


    [Serializable()]
    [XmlType("tFunctionKind", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public enum DmnFunctionKind
    {

        [XmlEnum("FEEL")]
        Feel,

        Java,

        [XmlEnum("ONNX")]
        Onnx,

        [XmlEnum("PMML")]
        Pmml,
    }


    [Serializable()]
    [XmlType("tInputData", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("inputData", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnInputData : DmnDRGElement
    {
        public DmnInputData() : base()
        {
        }

        [XmlElement("variable", Order = 0)]
        public DmnInformationItem Variable { get; set; } = new DmnInformationItem();
    }


    [Serializable()]
    [XmlType("tKnowledgeSource", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("knowledgeSource", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnKnowledgeSource : DmnDRGElement
    {

        [XmlIgnore()]
        private Collection<DmnAuthorityRequirement> _authorityRequirements;

        [XmlElement("authorityRequirement", Order = 0)]
        public Collection<DmnAuthorityRequirement> AuthorityRequirements
        {
            get
            {
                return _authorityRequirements;
            }
            private set
            {
                _authorityRequirements = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirements collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AuthorityRequirementsSpecified
        {
            get
            {
                return (this.AuthorityRequirements.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TKnowledgeSource" /> class.</para>
        /// </summary>
        public DmnKnowledgeSource() : base()
        {
            this._authorityRequirements = new Collection<DmnAuthorityRequirement>();
        }

        [XmlElement("type", Order = 1)]
        public string Type { get; set; } = string.Empty;

        [XmlElement("owner", Order = 2)]
        public DmnElementReference Owner { get; set; } = new DmnElementReference();

        [XmlAttribute("locationURI")]
        public string LocationUri { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tLiteralExpression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("literalExpression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnLiteralExpression : DmnExpression
    {
        public DmnLiteralExpression() : base()
        {
        }

        [XmlElement("text", Order = 0)]
        public string Text { get; set; } = string.Empty;

        [XmlElement("importedValues", Order = 1)]
        public DmnImportedValues ImportedValues { get; set; } = new DmnImportedValues();

        [XmlAttribute("expressionLanguage")]
        public string ExpressionLanguage { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tImportedValues", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnImportedValues : DmnImport
    {
        public DmnImportedValues() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("importedElement", Order = 0)]
        public string ImportedElement { get; set; } = string.Empty;

        [XmlAttribute("expressionLanguage")]
        public string ExpressionLanguage { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tInvocation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("invocation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnInvocation : DmnExpression
    {

        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("expression", Order = 0)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden

        [XmlIgnore()]
        private Collection<DmnBinding> _bindings;

        [XmlElement("binding", Order = 1)]
        public Collection<DmnBinding> Bindings
        {
            get
            {
                return _bindings;
            }
            private set
            {
                _bindings = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Bindings collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool BindingsSpecified
        {
            get
            {
                return (this.Bindings.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TInvocation" /> class.</para>
        /// </summary>
        public DmnInvocation() : base()
        {
            this._bindings = new Collection<DmnBinding>();
        }
    }


    [Serializable()]
    [XmlType("tBinding", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnBinding
    {
        public DmnBinding()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("parameter", Order = 0)]
        public DmnInformationItem Parameter { get; set; } = new DmnInformationItem();

        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("expression", Order = 1)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden
    }


    [Serializable()]
    [XmlType("tDecisionTable", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("decisionTable", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnDecisionTable : DmnExpression
    {

        [XmlIgnore()]
        private Collection<DmnInputClause> _inputs;

        [XmlElement("input", Order = 0)]
        public Collection<DmnInputClause> Inputs
        {
            get
            {
                return _inputs;
            }
            private set
            {
                _inputs = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Inputs collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool InputsSpecified
        {
            get
            {
                return (this.Inputs.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionTable" /> class.</para>
        /// </summary>
        public DmnDecisionTable() : base()
        {
            this._inputs = new Collection<DmnInputClause>();
            this._outputs = new Collection<DmnOutputClause>();
            this._annotations = new Collection<DmnRuleAnnotationClause>();
            this._rules = new Collection<DmnDecisionRule>();
        }

        [XmlIgnore()]
        private Collection<DmnOutputClause> _outputs;

        [Required(AllowEmptyStrings = true)]
        [XmlElement("output", Order = 1)]
        public Collection<DmnOutputClause> Outputs
        {
            get
            {
                return _outputs;
            }
            private set
            {
                _outputs = value;
            }
        }

        [XmlIgnore()]
        private Collection<DmnRuleAnnotationClause> _annotations;

        [XmlElement("annotation", Order = 2)]
        public Collection<DmnRuleAnnotationClause> Annotations
        {
            get
            {
                return _annotations;
            }
            private set
            {
                _annotations = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Annotations collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnnotationsSpecified
        {
            get
            {
                return (this.Annotations.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnDecisionRule> _rules;

        [XmlElement("rule", Order = 3)]
        public Collection<DmnDecisionRule> Rules
        {
            get
            {
                return _rules;
            }
            private set
            {
                _rules = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Rules collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool RulesSpecified
        {
            get
            {
                return (this.Rules.Count != 0);
            }
        }

        [XmlIgnore()]
        private DmnHitPolicy _hitPolicy = DmnHitPolicy.Unique;

        [DefaultValue(DmnHitPolicy.Unique)]
        [XmlAttribute("hitPolicy")]
        public DmnHitPolicy HitPolicy
        {
            get
            {
                return _hitPolicy;
            }
            set
            {
                _hitPolicy = value;
            }
        }


        [XmlAttribute("aggregation")]
        public DmnBuiltinAggregator AggregationValue { get; set; }

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Aggregation property is specified.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AggregationValueSpecified { get; set; } = false;

        [XmlIgnore()]
        public System.Nullable<DmnBuiltinAggregator> Aggregation
        {
            get
            {
                if (this.AggregationValueSpecified)
                {
                    return this.AggregationValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.AggregationValue = value.GetValueOrDefault();
                this.AggregationValueSpecified = value.HasValue;
            }
        }

        [XmlIgnore()]
        private DmnDecisionTableOrientation _preferredOrientation = DmnDecisionTableOrientation.RuleAsRow;

        [DefaultValue(DmnDecisionTableOrientation.RuleAsRow)]
        [XmlAttribute("preferredOrientation")]
        public DmnDecisionTableOrientation PreferredOrientation
        {
            get
            {
                return _preferredOrientation;
            }
            set
            {
                _preferredOrientation = value;
            }
        }

        [XmlAttribute("outputLabel")]
        public string OutputLabel { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tInputClause", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnInputClause : DmnElement
    {
        public DmnInputClause() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("inputExpression", Order = 0)]
        public DmnLiteralExpression InputExpression { get; set; } = new DmnLiteralExpression();

        [XmlElement("inputValues", Order = 1)]
        public DmnUnaryTests InputValues { get; set; } = new DmnUnaryTests();
    }


    [Serializable()]
    [XmlType("tOutputClause", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnOutputClause : DmnElement
    {
        public DmnOutputClause() : base()
        {
        }

        [XmlElement("outputValues", Order = 0)]
        public DmnUnaryTests OutputValues { get; set; } = new DmnUnaryTests();

        [XmlElement("defaultOutputEntry", Order = 1)]
        public DmnLiteralExpression DefaultOutputEntry { get; set; } = new DmnLiteralExpression();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tRuleAnnotationClause", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnRuleAnnotationClause
    {
        public DmnRuleAnnotationClause()
        {
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tDecisionRule", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnDecisionRule : DmnElement
    {

        [XmlIgnore()]
        private Collection<DmnUnaryTests> _inputEntries;

        [XmlElement("inputEntry", Order = 0)]
        public Collection<DmnUnaryTests> InputEntries
        {
            get
            {
                return _inputEntries;
            }
            private set
            {
                _inputEntries = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InputEntries collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool InputEntriesSpecified
        {
            get
            {
                return (this.InputEntries.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionRule" /> class.</para>
        /// </summary>
        public DmnDecisionRule() : base()
        {
            this._inputEntries = new Collection<DmnUnaryTests>();
            this._outputEntries = new Collection<DmnLiteralExpression>();
            this._annotationEntries = new Collection<DmnRuleAnnotation>();
        }

        [XmlIgnore()]
        private Collection<DmnLiteralExpression> _outputEntries;

        [Required(AllowEmptyStrings = true)]
        [XmlElement("outputEntry", Order = 1)]
        public Collection<DmnLiteralExpression> OutputEntries
        {
            get
            {
                return _outputEntries;
            }
            private set
            {
                _outputEntries = value;
            }
        }

        [XmlIgnore()]
        private Collection<DmnRuleAnnotation> _annotationEntries;

        [XmlElement("annotationEntry", Order = 2)]
        public Collection<DmnRuleAnnotation> AnnotationEntries
        {
            get
            {
                return _annotationEntries;
            }
            private set
            {
                _annotationEntries = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnnotationEntries collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool AnnotationEntriesSpecified
        {
            get
            {
                return (this.AnnotationEntries.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("tRuleAnnotation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnRuleAnnotation
    {
        public DmnRuleAnnotation()
        {
        }

        [XmlElement("text", Order = 0)]
        public string Text { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tHitPolicy", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public enum DmnHitPolicy
    {

        [XmlEnum("UNIQUE")]
        Unique,

        [XmlEnum("FIRST")]
        First,

        [XmlEnum("PRIORITY")]
        Priority,

        [XmlEnum("ANY")]
        Any,

        [XmlEnum("COLLECT")]
        Collect,

        [XmlEnum("RULE ORDER")]
        RuleOrder,

        [XmlEnum("OUTPUT ORDER")]
        OutputOrder,
    }


    [Serializable()]
    [XmlType("tBuiltinAggregator", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public enum DmnBuiltinAggregator
    {

        [XmlEnum("SUM")]
        Sum,

        [XmlEnum("COUNT")]
        Count,

        [XmlEnum("MIN")]
        Min,

        [XmlEnum("MAX")]
        Max,
    }


    [Serializable()]
    [XmlType("tDecisionTableOrientation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public enum DmnDecisionTableOrientation
    {

        [XmlEnum("Rule-as-Row")]
        RuleAsRow,

        [XmlEnum("Rule-as-Column")]
        RuleAsColumn,

        CrossTable,
    }


    [Serializable()]
    [XmlType("tGroup", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("group", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnGroup : DmnArtifact
    {
        public DmnGroup() : base()
        {
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tTextAnnotation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("textAnnotation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnTextAnnotation : DmnArtifact
    {
        public DmnTextAnnotation() : base()
        {
        }

        [XmlElement("text", Order = 0)]
        public string Text { get; set; } = string.Empty;

        [XmlIgnore()]
        private string _textFormat = "text/plain";

        [DefaultValue("text/plain")]
        [XmlAttribute("textFormat")]
        public string TextFormat
        {
            get
            {
                return _textFormat;
            }
            set
            {
                _textFormat = value;
            }
        }
    }


    [Serializable()]
    [XmlType("tAssociation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("association", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnAssociation : DmnArtifact
    {
        public DmnAssociation() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("sourceRef", Order = 0)]
        public DmnElementReference SourceRef { get; set; } = new DmnElementReference();

        [Required(AllowEmptyStrings = true)]
        [XmlElement("targetRef", Order = 1)]
        public DmnElementReference TargetRef { get; set; } = new DmnElementReference();

        [XmlIgnore()]
        private DmnAssociationDirection _associationDirection = DmnAssociationDirection.None;

        [DefaultValue(DmnAssociationDirection.None)]
        [XmlAttribute("associationDirection")]
        public DmnAssociationDirection AssociationDirection
        {
            get
            {
                return _associationDirection;
            }
            set
            {
                _associationDirection = value;
            }
        }
    }


    [Serializable()]
    [XmlType("tAssociationDirection", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public enum DmnAssociationDirection
    {

        None,

        One,

        Both,
    }


    [Serializable()]
    [XmlType("tContext", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("context", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnContext : DmnExpression
    {

        [XmlIgnore()]
        private Collection<DmnContextEntry> _contextEntries;

        [XmlElement("contextEntry", Order = 0)]
        public Collection<DmnContextEntry> ContextEntries
        {
            get
            {
                return _contextEntries;
            }
            private set
            {
                _contextEntries = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ContextEntries collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ContextEntriesSpecified
        {
            get
            {
                return (this.ContextEntries.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TContext" /> class.</para>
        /// </summary>
        public DmnContext() : base()
        {
            this._contextEntries = new Collection<DmnContextEntry>();
        }
    }


    [Serializable()]
    [XmlType("tContextEntry", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("contextEntry", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnContextEntry : DmnElement
    {
        public DmnContextEntry() : base()
        {
        }

        [XmlElement("variable", Order = 0)]
        public DmnInformationItem Variable { get; set; } = new DmnInformationItem();

        [Required(AllowEmptyStrings = true)]
        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 1)]
        [XmlElement("expression", Order = 1)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden
    }


    [Serializable()]
    [XmlType("tRelation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("relation", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnRelation : DmnExpression
    {

        [XmlIgnore()]
        private Collection<DmnInformationItem> _columns;

        [XmlElement("column", Order = 0)]
        public Collection<DmnInformationItem> Columns
        {
            get
            {
                return _columns;
            }
            private set
            {
                _columns = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Columns collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ColumnsSpecified
        {
            get
            {
                return (this.Columns.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TRelation" /> class.</para>
        /// </summary>
        public DmnRelation() : base()
        {
            this._columns = new Collection<DmnInformationItem>();
            this._rows = new Collection<DmnList>();
        }

        [XmlIgnore()]
        private Collection<DmnList> _rows;

        [XmlElement("row", Order = 1)]
        public Collection<DmnList> Rows
        {
            get
            {
                return _rows;
            }
            private set
            {
                _rows = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Rows collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool RowsSpecified
        {
            get
            {
                return (this.Rows.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("tList", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("list", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnList : DmnExpression
    {

        [XmlIgnore()]
        private Collection<DmnExpression> _expressions;

        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("expression", Order = 0)]
        public Collection<DmnExpression> Expressions
        {
            get
            {
                return _expressions;
            }
            private set
            {
                _expressions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Expressions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool ExpressionsSpecified
        {
            get
            {
                return (this.Expressions.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TList" /> class.</para>
        /// </summary>
        public DmnList() : base()
        {
            this._expressions = new Collection<DmnExpression>();
        }
    }


    [Serializable()]
    [XmlType("tDecisionService", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("decisionService", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnDecisionService : DmnInvocable
    {

        [XmlIgnore()]
        private Collection<DmnElementReference> _outputDecisions;

        [XmlElement("outputDecision", Order = 0)]
        public Collection<DmnElementReference> OutputDecisions
        {
            get
            {
                return _outputDecisions;
            }
            private set
            {
                _outputDecisions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the OutputDecisions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool OutputDecisionsSpecified
        {
            get
            {
                return (this.OutputDecisions.Count != 0);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionService" /> class.</para>
        /// </summary>
        public DmnDecisionService() : base()
        {
            this._outputDecisions = new Collection<DmnElementReference>();
            this._encapsulatedDecisions = new Collection<DmnElementReference>();
            this._inputDecisions = new Collection<DmnElementReference>();
            this._inputDatas = new Collection<DmnElementReference>();
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _encapsulatedDecisions;

        [XmlElement("encapsulatedDecision", Order = 1)]
        public Collection<DmnElementReference> EncapsulatedDecisions
        {
            get
            {
                return _encapsulatedDecisions;
            }
            private set
            {
                _encapsulatedDecisions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EncapsulatedDecisions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool EncapsulatedDecisionsSpecified
        {
            get
            {
                return (this.EncapsulatedDecisions.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _inputDecisions;

        [XmlElement("inputDecision", Order = 2)]
        public Collection<DmnElementReference> InputDecisions
        {
            get
            {
                return _inputDecisions;
            }
            private set
            {
                _inputDecisions = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InputDecisions collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool InputDecisionsSpecified
        {
            get
            {
                return (this.InputDecisions.Count != 0);
            }
        }

        [XmlIgnore()]
        private Collection<DmnElementReference> _inputDatas;

        [XmlElement("inputData", Order = 3)]
        public Collection<DmnElementReference> InputDatas
        {
            get
            {
                return _inputDatas;
            }
            private set
            {
                _inputDatas = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InputDatas collection is empty.</para>
        /// </summary>
        [XmlIgnore()]
        public bool InputDatasSpecified
        {
            get
            {
                return (this.InputDatas.Count != 0);
            }
        }
    }


    [Serializable()]
    [XmlType("tChildExpression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(DmnTypedChildExpression))]
    public record DmnChildExpression
    {
        public DmnChildExpression()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("literalExpression", Type = typeof(DmnLiteralExpression), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("invocation", Type = typeof(DmnInvocation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("decisionTable", Type = typeof(DmnDecisionTable), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("context", Type = typeof(DmnContext), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("functionDefinition", Type = typeof(DmnFunctionDefinition), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("relation", Type = typeof(DmnRelation), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("list", Type = typeof(DmnList), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("for", Type = typeof(DmnFor), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("every", Type = typeof(Every), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("some", Type = typeof(Some), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("conditional", Type = typeof(DmnConditional), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("filter", Type = typeof(DmnFilter), Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/", Order = 0)]
        [XmlElement("expression", Order = 0)]
        public DmnExpression Expression { get; set; } // Abstrakt: kann nicht initialisiert werden

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tTypedChildExpression", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public record DmnTypedChildExpression : DmnChildExpression
    {
        public DmnTypedChildExpression() : base()
        {
        }

        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tIterator", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(Every))]
    [XmlInclude(typeof(Some))]
    [XmlInclude(typeof(DmnFor))]
    [XmlInclude(typeof(DmnQuantified))]
    public record DmnIterator : DmnExpression
    {
        public DmnIterator() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("in", Order = 0)]
        public DmnTypedChildExpression In { get; set; } = new DmnTypedChildExpression();

        [XmlAttribute("iteratorVariable")]
        public string IteratorVariable { get; set; } = string.Empty;
    }


    [Serializable()]
    [XmlType("tFor", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("for", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnFor : DmnIterator
    {
        public DmnFor() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("return", Order = 0)]
        public DmnChildExpression Return { get; set; } = new DmnChildExpression();
    }


    [Serializable()]
    [XmlType("tQuantified", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlInclude(typeof(Every))]
    [XmlInclude(typeof(Some))]
    public record DmnQuantified : DmnIterator
    {
        public DmnQuantified() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("satisfies", Order = 0)]
        public DmnChildExpression Satisfies { get; set; } = new DmnChildExpression();
    }


    [Serializable()]
    [XmlType("tConditional", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conditional", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnConditional : DmnExpression
    {
        public DmnConditional() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("if", Order = 0)]
        public DmnChildExpression If { get; set; } = new DmnChildExpression();

        [Required(AllowEmptyStrings = true)]
        [XmlElement("then", Order = 1)]
        public DmnChildExpression Then { get; set; } = new DmnChildExpression();

        [Required(AllowEmptyStrings = true)]
        [XmlElement("else", Order = 2)]
        public DmnChildExpression Else { get; set; } = new DmnChildExpression();
    }


    [Serializable()]
    [XmlType("tFilter", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("filter", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record DmnFilter : DmnExpression
    {
        public DmnFilter() : base()
        {
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("in", Order = 0)]
        public DmnChildExpression In { get; set; } = new DmnChildExpression();

        [Required(AllowEmptyStrings = true)]
        [XmlElement("match", Order = 1)]
        public DmnChildExpression Match { get; set; } = new DmnChildExpression();
    }


    [Serializable()]
    [XmlType("some", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("some", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record Some : DmnQuantified
    {
        public Some() : base()
        {
        }
    }


    [Serializable()]
    [XmlType("every", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("every", Namespace = "https://www.omg.org/spec/DMN/20240513/MODEL/")]
    public record Every : DmnQuantified
    {
        public Every() : base()
        {
        }
    }
}