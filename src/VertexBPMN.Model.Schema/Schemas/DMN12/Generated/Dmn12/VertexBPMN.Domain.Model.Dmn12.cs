//namespace VertexBPMN.Domain.Model.Dmn
//{
    
    
//    /// <summary>
//    /// <para>Color is a data type that represents a color value in the RGB format.</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute("Color is a data type that represents a color value in the RGB format.")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Color", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("Color", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public partial class Color
//    {
        
//        /// <summary>
//        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
//        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
//        /// </summary>
//        [System.ComponentModel.DataAnnotations.RangeAttribute(typeof(int), "0", "255")]
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("red")]
//        public int Red { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
//        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
//        /// </summary>
//        [System.ComponentModel.DataAnnotations.RangeAttribute(typeof(int), "0", "255")]
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("green")]
//        public int Green { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Minimum inclusive value: 0.</para>
//        /// <para xml:lang="en">Maximum inclusive value: 255.</para>
//        /// </summary>
//        [System.ComponentModel.DataAnnotations.RangeAttribute(typeof(int), "0", "255")]
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("blue")]
//        public int Blue { get; set; }
//    }
    
//    /// <summary>
//    /// <para>A Point specifies an location in some x-y coordinate system.</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute("A Point specifies an location in some x-y coordinate system.")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Point", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("Point", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public partial class Point
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("x")]
//        public double X { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("y")]
//        public double Y { get; set; }
//    }
    
//    /// <summary>
//    /// <para>Dimension specifies two lengths (width and height) along the x and y axes in some x-y coordinate system.</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute(("Dimension specifies two lengths (width and height) along the x and y axes in some" +
//        " x-y coordinate system."))]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Dimension", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("Dimension", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public partial class Dimension
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("width")]
//        public double Width { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("height")]
//        public double Height { get; set; }
//    }
    
//    /// <summary>
//    /// <para>Bounds specifies a rectangular area in some x-y coordinate system that is defined by a location (x and y) and a size (width and height).</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute(("Bounds specifies a rectangular area in some x-y coordinate system that is defined" +
//        " by a location (x and y) and a size (width and height)."))]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Bounds", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("Bounds", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public partial class Bounds
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("x")]
//        public double X { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("y")]
//        public double Y { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("width")]
//        public double Width { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("height")]
//        public double Height { get; set; }
//    }
    
//    /// <summary>
//    /// <para>AlignmentKind enumerates the possible options for alignment for layout purposes.</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute("AlignmentKind enumerates the possible options for alignment for layout purposes.")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("AlignmentKind", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public enum AlignmentKind
//    {
        
//        [System.Xml.Serialization.XmlEnumAttribute("start")]
//        Start,
        
//        [System.Xml.Serialization.XmlEnumAttribute("end")]
//        End,
        
//        [System.Xml.Serialization.XmlEnumAttribute("center")]
//        Center,
//    }
    
//    /// <summary>
//    /// <para>KnownColor is an enumeration of 17 known colors.</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute("KnownColor is an enumeration of 17 known colors.")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("KnownColor", Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//    public enum KnownColor
//    {
        
//        /// <summary>
//        /// <para>a color with a value of #800000</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #800000")]
//        [System.Xml.Serialization.XmlEnumAttribute("maroon")]
//        Maroon,
        
//        /// <summary>
//        /// <para>a color with a value of #FF0000</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #FF0000")]
//        [System.Xml.Serialization.XmlEnumAttribute("red")]
//        Red,
        
//        /// <summary>
//        /// <para>a color with a value of #FFA500</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #FFA500")]
//        [System.Xml.Serialization.XmlEnumAttribute("orange")]
//        Orange,
        
//        /// <summary>
//        /// <para>a color with a value of #FFFF00</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #FFFF00")]
//        [System.Xml.Serialization.XmlEnumAttribute("yellow")]
//        Yellow,
        
//        /// <summary>
//        /// <para>a color with a value of #808000</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #808000")]
//        [System.Xml.Serialization.XmlEnumAttribute("olive")]
//        Olive,
        
//        /// <summary>
//        /// <para>a color with a value of #800080</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #800080")]
//        [System.Xml.Serialization.XmlEnumAttribute("purple")]
//        Purple,
        
//        /// <summary>
//        /// <para>a color with a value of #FF00FF</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #FF00FF")]
//        [System.Xml.Serialization.XmlEnumAttribute("fuchsia")]
//        Fuchsia,
        
//        /// <summary>
//        /// <para>a color with a value of #FFFFFF</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #FFFFFF")]
//        [System.Xml.Serialization.XmlEnumAttribute("white")]
//        White,
        
//        /// <summary>
//        /// <para>a color with a value of #00FF00</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #00FF00")]
//        [System.Xml.Serialization.XmlEnumAttribute("lime")]
//        Lime,
        
//        /// <summary>
//        /// <para>a color with a value of #008000</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #008000")]
//        [System.Xml.Serialization.XmlEnumAttribute("green")]
//        Green,
        
//        /// <summary>
//        /// <para>a color with a value of #000080</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #000080")]
//        [System.Xml.Serialization.XmlEnumAttribute("navy")]
//        Navy,
        
//        /// <summary>
//        /// <para>a color with a value of #0000FF</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #0000FF")]
//        [System.Xml.Serialization.XmlEnumAttribute("blue")]
//        Blue,
        
//        /// <summary>
//        /// <para>a color with a value of #00FFFF</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #00FFFF")]
//        [System.Xml.Serialization.XmlEnumAttribute("aqua")]
//        Aqua,
        
//        /// <summary>
//        /// <para>a color with a value of #008080</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #008080")]
//        [System.Xml.Serialization.XmlEnumAttribute("teal")]
//        Teal,
        
//        /// <summary>
//        /// <para>a color with a value of #000000</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #000000")]
//        [System.Xml.Serialization.XmlEnumAttribute("black")]
//        Black,
        
//        /// <summary>
//        /// <para>a color with a value of #C0C0C0</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #C0C0C0")]
//        [System.Xml.Serialization.XmlEnumAttribute("silver")]
//        Silver,
        
//        /// <summary>
//        /// <para>a color with a value of #808080</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a color with a value of #808080")]
//        [System.Xml.Serialization.XmlEnumAttribute("gray")]
//        Gray,
//    }
    
//    /// <summary>
//    /// <para>DiagramElement is the abstract super type of all elements in diagrams, including diagrams themselves. When contained in a diagram, diagram elements are laid out relative to the diagram's origin.</para>
//    /// <para>This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute(@"DiagramElement is the abstract super type of all elements in diagrams, including diagrams themselves. When contained in a diagram, diagram elements are laid out relative to the diagram's origin. This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DiagramElement", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNDiagramElement", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Diagram))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnDecisionServiceDividerLine))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnDiagram))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnEdge))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnLabel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnShape))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Edge))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Shape))]
//    public abstract partial class DiagramElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("extension", Order=0)]
//        public DiagramElementExtension Extension { get; set; }
        
//        /// <summary>
//        /// <para>an optional locally-owned style for this diagram element.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("an optional locally-owned style for this diagram element.")]
//        [System.Xml.Serialization.XmlElementAttribute("DMNStyle", Type=typeof(VertexBPMN.Domain.Model.Dmn12.DmnStyle), Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("Style", Order=1)]
//        public Style Style { get; set; }
        
//        /// <summary>
//        /// <para>a reference to an optional shared style element for this diagram element.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("a reference to an optional shared style element for this diagram element.")]
//        [System.Xml.Serialization.XmlAttributeAttribute("sharedStyle")]
//        public string SharedStyle { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("id")]
//        public string Id { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> _anyAttribute;
        
//        [System.Xml.Serialization.XmlAnyAttributeAttribute(Order=4)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> AnyAttribute
//        {
//            get
//            {
//                return _anyAttribute;
//            }
//            private set
//            {
//                _anyAttribute = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnyAttributeSpecified
//        {
//            get
//            {
//                return (this.AnyAttribute.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="DiagramElement" /> class.</para>
//        /// </summary>
//        public DiagramElement()
//        {
//            this._anyAttribute = new System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DiagramElementExtension", Namespace="http://www.omg.org/spec/DMN/20180521/DI/", AnonymousType=true)]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class DiagramElementExtension
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlElement> _any;
        
//        [System.Xml.Serialization.XmlAnyElementAttribute(Order=0)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlElement> Any
//        {
//            get
//            {
//                return _any;
//            }
//            private set
//            {
//                _any = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnySpecified
//        {
//            get
//            {
//                return (this.Any.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="DiagramElementExtension" /> class.</para>
//        /// </summary>
//        public DiagramElementExtension()
//        {
//            this._any = new System.Collections.ObjectModel.Collection<System.Xml.XmlElement>();
//        }
//    }
    
//    /// <summary>
//    /// <para>Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves.</para>
//    /// <para>This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence</para>
//    /// </summary>
//    [System.ComponentModel.DescriptionAttribute(@"Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves. This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence")]
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Style", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("Style", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnStyle))]
//    public abstract partial class Style
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("extension", Order=0)]
//        public StyleExtension Extension { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("id")]
//        public string Id { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> _anyAttribute;
        
//        [System.Xml.Serialization.XmlAnyAttributeAttribute(Order=2)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> AnyAttribute
//        {
//            get
//            {
//                return _anyAttribute;
//            }
//            private set
//            {
//                _anyAttribute = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnyAttributeSpecified
//        {
//            get
//            {
//                return (this.AnyAttribute.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="Style" /> class.</para>
//        /// </summary>
//        public Style()
//        {
//            this._anyAttribute = new System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("StyleExtension", Namespace="http://www.omg.org/spec/DMN/20180521/DI/", AnonymousType=true)]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class StyleExtension
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlElement> _any;
        
//        [System.Xml.Serialization.XmlAnyElementAttribute(Order=0)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlElement> Any
//        {
//            get
//            {
//                return _any;
//            }
//            private set
//            {
//                _any = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnySpecified
//        {
//            get
//            {
//                return (this.Any.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="StyleExtension" /> class.</para>
//        /// </summary>
//        public StyleExtension()
//        {
//            this._any = new System.Collections.ObjectModel.Collection<System.Xml.XmlElement>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Diagram", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnDiagram))]
//    public abstract partial class Diagram : DiagramElement
//    {
        
//        /// <summary>
//        /// <para>the name of the diagram.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("the name of the diagram.")]
//        [System.Xml.Serialization.XmlAttributeAttribute("name")]
//        public string Name { get; set; }
        
//        /// <summary>
//        /// <para>the documentation of the diagram.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("the documentation of the diagram.")]
//        [System.Xml.Serialization.XmlAttributeAttribute("documentation")]
//        public string Documentation { get; set; }
        
//        /// <summary>
//        /// <para>the resolution of the diagram expressed in user units per inch.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("the resolution of the diagram expressed in user units per inch.")]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("resolution")]
//        public double ResolutionValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the Resolution property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool ResolutionValueSpecified { get; set; }
        
//        /// <summary>
//        /// <para>the resolution of the diagram expressed in user units per inch.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<double> Resolution
//        {
//            get
//            {
//                if (this.ResolutionValueSpecified)
//                {
//                    return this.ResolutionValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.ResolutionValue = value.GetValueOrDefault();
//                this.ResolutionValueSpecified = value.HasValue;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Shape", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnLabel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnShape))]
//    public abstract partial class Shape : DiagramElement
//    {
        
//        /// <summary>
//        /// <para>the optional bounds of the shape relative to the origin of its nesting plane.</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute("the optional bounds of the shape relative to the origin of its nesting plane.")]
//        [System.Xml.Serialization.XmlElementAttribute("Bounds", Order=0, Namespace="http://www.omg.org/spec/DMN/20180521/DC/")]
//        public VertexBPMN.Domain.Model.Dmn12.Bounds Bounds { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("Edge", Namespace="http://www.omg.org/spec/DMN/20180521/DI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnDecisionServiceDividerLine))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(VertexBPMN.Domain.Model.Dmn12.DmnEdge))]
//    public abstract partial class Edge : DiagramElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.Point> _waypoint;
        
//        /// <summary>
//        /// <para>an optional list of points relative to the origin of the nesting diagram that specifies the connected line segments of the edge</para>
//        /// </summary>
//        [System.ComponentModel.DescriptionAttribute(("an optional list of points relative to the origin of the nesting diagram that spe" +
//            "cifies the connected line segments of the edge"))]
//        [System.Xml.Serialization.XmlElementAttribute("waypoint", Order=0)]
//        public System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.Point> Waypoint
//        {
//            get
//            {
//                return _waypoint;
//            }
//            private set
//            {
//                _waypoint = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Waypoint collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool WaypointSpecified
//        {
//            get
//            {
//                return (this.Waypoint.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="Edge" /> class.</para>
//        /// </summary>
//        public Edge()
//        {
//            this._waypoint = new System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.Point>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNDI", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNDI", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class Dmndi
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<DmnDiagram> _dmnDiagram;
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNDiagram", Order=0)]
//        public System.Collections.ObjectModel.Collection<DmnDiagram> DmnDiagram
//        {
//            get
//            {
//                return _dmnDiagram;
//            }
//            private set
//            {
//                _dmnDiagram = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DmnDiagram collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DmnDiagramSpecified
//        {
//            get
//            {
//                return (this.DmnDiagram.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="Dmndi" /> class.</para>
//        /// </summary>
//        public Dmndi()
//        {
//            this._dmnDiagram = new System.Collections.ObjectModel.Collection<DmnDiagram>();
//            this._dmnStyle = new System.Collections.ObjectModel.Collection<DmnStyle>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<DmnStyle> _dmnStyle;
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNStyle", Order=1)]
//        public System.Collections.ObjectModel.Collection<DmnStyle> DmnStyle
//        {
//            get
//            {
//                return _dmnStyle;
//            }
//            private set
//            {
//                _dmnStyle = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DmnStyle collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DmnStyleSpecified
//        {
//            get
//            {
//                return (this.DmnStyle.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNDiagram", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNDiagram", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnDiagram : VertexBPMN.Domain.Model.Dmn12.Diagram
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("Size", Order=0)]
//        public VertexBPMN.Domain.Model.Dmn12.Dimension Size { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.DiagramElement> _dmnDiagramElement;
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNShape", Type=typeof(DmnShape), Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("DMNEdge", Type=typeof(DmnEdge), Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("DMNDiagramElement", Order=1)]
//        public System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.DiagramElement> DmnDiagramElement
//        {
//            get
//            {
//                return _dmnDiagramElement;
//            }
//            private set
//            {
//                _dmnDiagramElement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DmnDiagramElement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DmnDiagramElementSpecified
//        {
//            get
//            {
//                return (this.DmnDiagramElement.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="DmnDiagram" /> class.</para>
//        /// </summary>
//        public DmnDiagram()
//        {
//            this._dmnDiagramElement = new System.Collections.ObjectModel.Collection<VertexBPMN.Domain.Model.Dmn12.DiagramElement>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private bool _useAlternativeInputDataShape = false;
        
//        [System.ComponentModel.DefaultValueAttribute(false)]
//        [System.Xml.Serialization.XmlAttributeAttribute("useAlternativeInputDataShape")]
//        public bool UseAlternativeInputDataShape
//        {
//            get
//            {
//                return _useAlternativeInputDataShape;
//            }
//            set
//            {
//                _useAlternativeInputDataShape = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNStyle", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNStyle", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnStyle : VertexBPMN.Domain.Model.Dmn12.Style
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("FillColor", Order=0)]
//        public VertexBPMN.Domain.Model.Dmn12.Color FillColor { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("StrokeColor", Order=1)]
//        public VertexBPMN.Domain.Model.Dmn12.Color StrokeColor { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("FontColor", Order=2)]
//        public VertexBPMN.Domain.Model.Dmn12.Color FontColor { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("fontFamily")]
//        public string FontFamily { get; set; }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("fontSize")]
//        public double FontSizeValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the FontSize property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool FontSizeValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<double> FontSize
//        {
//            get
//            {
//                if (this.FontSizeValueSpecified)
//                {
//                    return this.FontSizeValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.FontSizeValue = value.GetValueOrDefault();
//                this.FontSizeValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("fontItalic")]
//        public bool FontItalicValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the FontItalic property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool FontItalicValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<bool> FontItalic
//        {
//            get
//            {
//                if (this.FontItalicValueSpecified)
//                {
//                    return this.FontItalicValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.FontItalicValue = value.GetValueOrDefault();
//                this.FontItalicValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("fontBold")]
//        public bool FontBoldValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the FontBold property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool FontBoldValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<bool> FontBold
//        {
//            get
//            {
//                if (this.FontBoldValueSpecified)
//                {
//                    return this.FontBoldValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.FontBoldValue = value.GetValueOrDefault();
//                this.FontBoldValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("fontUnderline")]
//        public bool FontUnderlineValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the FontUnderline property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool FontUnderlineValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<bool> FontUnderline
//        {
//            get
//            {
//                if (this.FontUnderlineValueSpecified)
//                {
//                    return this.FontUnderlineValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.FontUnderlineValue = value.GetValueOrDefault();
//                this.FontUnderlineValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("fontStrikeThrough")]
//        public bool FontStrikeThroughValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the FontStrikeThrough property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool FontStrikeThroughValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<bool> FontStrikeThrough
//        {
//            get
//            {
//                if (this.FontStrikeThroughValueSpecified)
//                {
//                    return this.FontStrikeThroughValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.FontStrikeThroughValue = value.GetValueOrDefault();
//                this.FontStrikeThroughValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("labelHorizontalAlignement")]
//        public VertexBPMN.Domain.Model.Dmn12.AlignmentKind LabelHorizontalAlignementValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelHorizontalAlignement property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool LabelHorizontalAlignementValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<VertexBPMN.Domain.Model.Dmn12.AlignmentKind> LabelHorizontalAlignement
//        {
//            get
//            {
//                if (this.LabelHorizontalAlignementValueSpecified)
//                {
//                    return this.LabelHorizontalAlignementValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.LabelHorizontalAlignementValue = value.GetValueOrDefault();
//                this.LabelHorizontalAlignementValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("labelVerticalAlignment")]
//        public VertexBPMN.Domain.Model.Dmn12.AlignmentKind LabelVerticalAlignmentValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelVerticalAlignment property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool LabelVerticalAlignmentValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<VertexBPMN.Domain.Model.Dmn12.AlignmentKind> LabelVerticalAlignment
//        {
//            get
//            {
//                if (this.LabelVerticalAlignmentValueSpecified)
//                {
//                    return this.LabelVerticalAlignmentValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.LabelVerticalAlignmentValue = value.GetValueOrDefault();
//                this.LabelVerticalAlignmentValueSpecified = value.HasValue;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNShape", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNShape", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnShape : VertexBPMN.Domain.Model.Dmn12.Shape
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNLabel", Order=0)]
//        public DmnLabel DmnLabel { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNDecisionServiceDividerLine", Order=1)]
//        public DmnDecisionServiceDividerLine DmnDecisionServiceDividerLine { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("dmnElementRef")]
//        public System.Xml.XmlQualifiedName DmnElementRef { get; set; }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("isListedInputData")]
//        public bool IsListedInputDataValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the IsListedInputData property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool IsListedInputDataValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<bool> IsListedInputData
//        {
//            get
//            {
//                if (this.IsListedInputDataValueSpecified)
//                {
//                    return this.IsListedInputDataValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.IsListedInputDataValue = value.GetValueOrDefault();
//                this.IsListedInputDataValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private bool _isCollapsed = false;
        
//        [System.ComponentModel.DefaultValueAttribute(false)]
//        [System.Xml.Serialization.XmlAttributeAttribute("isCollapsed")]
//        public bool IsCollapsed
//        {
//            get
//            {
//                return _isCollapsed;
//            }
//            set
//            {
//                _isCollapsed = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNLabel", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNLabel", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnLabel : VertexBPMN.Domain.Model.Dmn12.Shape
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("Text", Order=0)]
//        public string Text { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNDecisionServiceDividerLine", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNDecisionServiceDividerLine", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnDecisionServiceDividerLine : VertexBPMN.Domain.Model.Dmn12.Edge
//    {
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("DMNEdge", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNEdge", Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//    public partial class DmnEdge : VertexBPMN.Domain.Model.Dmn12.Edge
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNLabel", Order=0)]
//        public DmnLabel DmnLabel { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("dmnElementRef")]
//        public System.Xml.XmlQualifiedName DmnElementRef { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("sourceElement")]
//        public System.Xml.XmlQualifiedName SourceElement { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("targetElement")]
//        public System.Xml.XmlQualifiedName TargetElement { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDMNElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("DMNElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Every))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Some))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TArtifact))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TAssociation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TAuthorityRequirement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessContextElement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessKnowledgeModel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TConditional))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TContext))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TContextEntry))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecision))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionRule))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionService))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionTable))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDefinitions))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TdrgElement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TElementCollection))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TExpression))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFilter))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFor))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFunctionDefinition))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFunctionItem))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TGroup))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TImport))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TImportedValues))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInformationItem))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInformationRequirement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInputClause))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInputData))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInvocable))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInvocation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TItemDefinition))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TIterator))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TKnowledgeRequirement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TKnowledgeSource))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TList))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TLiteralExpression))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TNamedElement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TOrganizationUnit))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TOutputClause))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TPerformanceIndicator))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TQuantified))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TRelation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TTextAnnotation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TUnaryTests))]
//    public partial class TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("description", Order=0)]
//        public string Description { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("extensionElements", Order=1)]
//        public TdmnElementExtensionElements ExtensionElements { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("id")]
//        public string Id { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("label")]
//        public string Label { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> _anyAttribute;
        
//        [System.Xml.Serialization.XmlAnyAttributeAttribute(Order=4)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute> AnyAttribute
//        {
//            get
//            {
//                return _anyAttribute;
//            }
//            private set
//            {
//                _anyAttribute = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnyAttributeSpecified
//        {
//            get
//            {
//                return (this.AnyAttribute.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TdmnElement" /> class.</para>
//        /// </summary>
//        public TdmnElement()
//        {
//            this._anyAttribute = new System.Collections.ObjectModel.Collection<System.Xml.XmlAttribute>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("TdmnElementExtensionElements", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", AnonymousType=true)]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TdmnElementExtensionElements
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<System.Xml.XmlElement> _any;
        
//        [System.Xml.Serialization.XmlAnyElementAttribute(Order=0)]
//        public System.Collections.ObjectModel.Collection<System.Xml.XmlElement> Any
//        {
//            get
//            {
//                return _any;
//            }
//            private set
//            {
//                _any = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnySpecified
//        {
//            get
//            {
//                return (this.Any.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TdmnElementExtensionElements" /> class.</para>
//        /// </summary>
//        public TdmnElementExtensionElements()
//        {
//            this._any = new System.Collections.ObjectModel.Collection<System.Xml.XmlElement>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tNamedElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("namedElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessContextElement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessKnowledgeModel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecision))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionService))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDefinitions))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TdrgElement))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TElementCollection))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TImport))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TImportedValues))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInformationItem))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInputData))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInvocable))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TItemDefinition))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TKnowledgeSource))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TOrganizationUnit))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TPerformanceIndicator))]
//    public partial class TNamedElement : TdmnElement
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("name")]
//        public string Name { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDMNElementReference", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TdmnElementReference
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("href")]
//        public string Href { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDefinitions", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("definitions", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TDefinitions : TNamedElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TImport> _import;
        
//        [System.Xml.Serialization.XmlElementAttribute("import", Order=0)]
//        public System.Collections.ObjectModel.Collection<TImport> Import
//        {
//            get
//            {
//                return _import;
//            }
//            private set
//            {
//                _import = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Import collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ImportSpecified
//        {
//            get
//            {
//                return (this.Import.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDefinitions" /> class.</para>
//        /// </summary>
//        public TDefinitions()
//        {
//            this._import = new System.Collections.ObjectModel.Collection<TImport>();
//            this._itemDefinition = new System.Collections.ObjectModel.Collection<TItemDefinition>();
//            this._drgElement = new System.Collections.ObjectModel.Collection<TdrgElement>();
//            this._artifact = new System.Collections.ObjectModel.Collection<TArtifact>();
//            this._elementCollection = new System.Collections.ObjectModel.Collection<TElementCollection>();
//            this._businessContextElement = new System.Collections.ObjectModel.Collection<TBusinessContextElement>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TItemDefinition> _itemDefinition;
        
//        [System.Xml.Serialization.XmlElementAttribute("itemDefinition", Order=1)]
//        public System.Collections.ObjectModel.Collection<TItemDefinition> ItemDefinition
//        {
//            get
//            {
//                return _itemDefinition;
//            }
//            private set
//            {
//                _itemDefinition = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ItemDefinition collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ItemDefinitionSpecified
//        {
//            get
//            {
//                return (this.ItemDefinition.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdrgElement> _drgElement;
        
//        [System.Xml.Serialization.XmlElementAttribute("decision", Type=typeof(TDecision), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("invocable", Type=typeof(TInvocable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("businessKnowledgeModel", Type=typeof(TBusinessKnowledgeModel), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionService", Type=typeof(TDecisionService), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("inputData", Type=typeof(TInputData), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("knowledgeSource", Type=typeof(TKnowledgeSource), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=2)]
//        [System.Xml.Serialization.XmlElementAttribute("drgElement", Order=2)]
//        public System.Collections.ObjectModel.Collection<TdrgElement> DrgElement
//        {
//            get
//            {
//                return _drgElement;
//            }
//            private set
//            {
//                _drgElement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DrgElement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DrgElementSpecified
//        {
//            get
//            {
//                return (this.DrgElement.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TArtifact> _artifact;
        
//        [System.Xml.Serialization.XmlElementAttribute("group", Type=typeof(TGroup), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=3)]
//        [System.Xml.Serialization.XmlElementAttribute("textAnnotation", Type=typeof(TTextAnnotation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=3)]
//        [System.Xml.Serialization.XmlElementAttribute("association", Type=typeof(TAssociation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=3)]
//        [System.Xml.Serialization.XmlElementAttribute("artifact", Order=3)]
//        public System.Collections.ObjectModel.Collection<TArtifact> Artifact
//        {
//            get
//            {
//                return _artifact;
//            }
//            private set
//            {
//                _artifact = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Artifact collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ArtifactSpecified
//        {
//            get
//            {
//                return (this.Artifact.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TElementCollection> _elementCollection;
        
//        [System.Xml.Serialization.XmlElementAttribute("elementCollection", Order=4)]
//        public System.Collections.ObjectModel.Collection<TElementCollection> ElementCollection
//        {
//            get
//            {
//                return _elementCollection;
//            }
//            private set
//            {
//                _elementCollection = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ElementCollection collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ElementCollectionSpecified
//        {
//            get
//            {
//                return (this.ElementCollection.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TBusinessContextElement> _businessContextElement;
        
//        [System.Xml.Serialization.XmlElementAttribute("performanceIndicator", Type=typeof(TPerformanceIndicator), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=5)]
//        [System.Xml.Serialization.XmlElementAttribute("organizationUnit", Type=typeof(TOrganizationUnit), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=5)]
//        [System.Xml.Serialization.XmlElementAttribute("businessContextElement", Order=5)]
//        public System.Collections.ObjectModel.Collection<TBusinessContextElement> BusinessContextElement
//        {
//            get
//            {
//                return _businessContextElement;
//            }
//            private set
//            {
//                _businessContextElement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the BusinessContextElement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool BusinessContextElementSpecified
//        {
//            get
//            {
//                return (this.BusinessContextElement.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlElementAttribute("DMNDI", Order=6, Namespace="https://www.omg.org/spec/DMN/20230324/DMNDI/")]
//        public VertexBPMN.Domain.Model.Dmn12.Dmndi Dmndi { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private string _expressionLanguage = "https://www.omg.org/spec/DMN/20240513/FEEL/";
        
//        [System.ComponentModel.DefaultValueAttribute("https://www.omg.org/spec/DMN/20240513/FEEL/")]
//        [System.Xml.Serialization.XmlAttributeAttribute("expressionLanguage")]
//        public string ExpressionLanguage
//        {
//            get
//            {
//                return _expressionLanguage;
//            }
//            set
//            {
//                _expressionLanguage = value;
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private string _typeLanguage = "https://www.omg.org/spec/DMN/20240513/FEEL/";
        
//        [System.ComponentModel.DefaultValueAttribute("https://www.omg.org/spec/DMN/20240513/FEEL/")]
//        [System.Xml.Serialization.XmlAttributeAttribute("typeLanguage")]
//        public string TypeLanguage
//        {
//            get
//            {
//                return _typeLanguage;
//            }
//            set
//            {
//                _typeLanguage = value;
//            }
//        }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("namespace")]
//        public string Namespace { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("exporter")]
//        public string Exporter { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("exporterVersion")]
//        public string ExporterVersion { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tImport", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("import", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TImportedValues))]
//    public partial class TImport : TNamedElement
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("namespace")]
//        public string Namespace { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("locationURI")]
//        public string LocationUri { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlAttributeAttribute("importType")]
//        public string ImportType { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tItemDefinition", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("itemDefinition", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TItemDefinition : TNamedElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("typeRef", Order=0)]
//        public string TypeRef { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("allowedValues", Order=1)]
//        public TUnaryTests AllowedValues { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("typeConstraint", Order=2)]
//        public TUnaryTests TypeConstraint { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TItemDefinition> _itemComponent;
        
//        [System.Xml.Serialization.XmlElementAttribute("itemComponent", Order=3)]
//        public System.Collections.ObjectModel.Collection<TItemDefinition> ItemComponent
//        {
//            get
//            {
//                return _itemComponent;
//            }
//            private set
//            {
//                _itemComponent = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ItemComponent collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ItemComponentSpecified
//        {
//            get
//            {
//                return (this.ItemComponent.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TItemDefinition" /> class.</para>
//        /// </summary>
//        public TItemDefinition()
//        {
//            this._itemComponent = new System.Collections.ObjectModel.Collection<TItemDefinition>();
//        }
        
//        [System.Xml.Serialization.XmlElementAttribute("functionItem", Order=4)]
//        public TFunctionItem FunctionItem { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("typeLanguage")]
//        public string TypeLanguage { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private bool _isCollection = false;
        
//        [System.ComponentModel.DefaultValueAttribute(false)]
//        [System.Xml.Serialization.XmlAttributeAttribute("isCollection")]
//        public bool IsCollection
//        {
//            get
//            {
//                return _isCollection;
//            }
//            set
//            {
//                _isCollection = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tUnaryTests", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TUnaryTests : TExpression
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("text", Order=0)]
//        public string Text { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("expressionLanguage")]
//        public string ExpressionLanguage { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tExpression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("expression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Every))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Some))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TConditional))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TContext))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionTable))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFilter))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFor))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFunctionDefinition))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInvocation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TIterator))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TList))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TLiteralExpression))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TQuantified))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TRelation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TUnaryTests))]
//    public partial class TExpression : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("typeRef")]
//        public string TypeRef { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tFunctionItem", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("functionItem", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TFunctionItem : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TInformationItem> _parameters;
        
//        [System.Xml.Serialization.XmlElementAttribute("parameters", Order=0)]
//        public System.Collections.ObjectModel.Collection<TInformationItem> Parameters
//        {
//            get
//            {
//                return _parameters;
//            }
//            private set
//            {
//                _parameters = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Parameters collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ParametersSpecified
//        {
//            get
//            {
//                return (this.Parameters.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TFunctionItem" /> class.</para>
//        /// </summary>
//        public TFunctionItem()
//        {
//            this._parameters = new System.Collections.ObjectModel.Collection<TInformationItem>();
//        }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("outputTypeRef")]
//        public string OutputTypeRef { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInformationItem", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("informationItem", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TInformationItem : TNamedElement
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("typeRef")]
//        public string TypeRef { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDRGElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("drgElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessKnowledgeModel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecision))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionService))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInputData))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TInvocable))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TKnowledgeSource))]
//    public partial class TdrgElement : TNamedElement
//    {
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tArtifact", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("artifact", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TAssociation))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TGroup))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TTextAnnotation))]
//    public partial class TArtifact : TdmnElement
//    {
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tElementCollection", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("elementCollection", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TElementCollection : TNamedElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _drgElement;
        
//        [System.Xml.Serialization.XmlElementAttribute("decision", Type=typeof(TDecision), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("invocable", Type=typeof(TInvocable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("businessKnowledgeModel", Type=typeof(TBusinessKnowledgeModel), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionService", Type=typeof(TDecisionService), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("inputData", Type=typeof(TInputData), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("knowledgeSource", Type=typeof(TKnowledgeSource), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("drgElement", Order=0)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> DrgElement
//        {
//            get
//            {
//                return _drgElement;
//            }
//            private set
//            {
//                _drgElement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DrgElement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DrgElementSpecified
//        {
//            get
//            {
//                return (this.DrgElement.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TElementCollection" /> class.</para>
//        /// </summary>
//        public TElementCollection()
//        {
//            this._drgElement = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tBusinessContextElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("businessContextElement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TOrganizationUnit))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TPerformanceIndicator))]
//    public partial class TBusinessContextElement : TNamedElement
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("URI")]
//        public string Uri { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDecision", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("decision", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TDecision : TdrgElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("question", Order=0)]
//        public string Question { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("allowedAnswers", Order=1)]
//        public string AllowedAnswers { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("variable", Order=2)]
//        public TInformationItem Variable { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TInformationRequirement> _informationRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("informationRequirement", Order=3)]
//        public System.Collections.ObjectModel.Collection<TInformationRequirement> InformationRequirement
//        {
//            get
//            {
//                return _informationRequirement;
//            }
//            private set
//            {
//                _informationRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the InformationRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool InformationRequirementSpecified
//        {
//            get
//            {
//                return (this.InformationRequirement.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecision" /> class.</para>
//        /// </summary>
//        public TDecision()
//        {
//            this._informationRequirement = new System.Collections.ObjectModel.Collection<TInformationRequirement>();
//            this._knowledgeRequirement = new System.Collections.ObjectModel.Collection<TKnowledgeRequirement>();
//            this._authorityRequirement = new System.Collections.ObjectModel.Collection<TAuthorityRequirement>();
//            this._supportedObjective = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._impactedPerformanceIndicator = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._decisionMaker = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._decisionOwner = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._usingProcess = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._usingTask = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TKnowledgeRequirement> _knowledgeRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("knowledgeRequirement", Order=4)]
//        public System.Collections.ObjectModel.Collection<TKnowledgeRequirement> KnowledgeRequirement
//        {
//            get
//            {
//                return _knowledgeRequirement;
//            }
//            private set
//            {
//                _knowledgeRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the KnowledgeRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool KnowledgeRequirementSpecified
//        {
//            get
//            {
//                return (this.KnowledgeRequirement.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TAuthorityRequirement> _authorityRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("authorityRequirement", Order=5)]
//        public System.Collections.ObjectModel.Collection<TAuthorityRequirement> AuthorityRequirement
//        {
//            get
//            {
//                return _authorityRequirement;
//            }
//            private set
//            {
//                _authorityRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AuthorityRequirementSpecified
//        {
//            get
//            {
//                return (this.AuthorityRequirement.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _supportedObjective;
        
//        [System.Xml.Serialization.XmlElementAttribute("supportedObjective", Order=6)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> SupportedObjective
//        {
//            get
//            {
//                return _supportedObjective;
//            }
//            private set
//            {
//                _supportedObjective = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the SupportedObjective collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool SupportedObjectiveSpecified
//        {
//            get
//            {
//                return (this.SupportedObjective.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _impactedPerformanceIndicator;
        
//        [System.Xml.Serialization.XmlElementAttribute("impactedPerformanceIndicator", Order=7)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> ImpactedPerformanceIndicator
//        {
//            get
//            {
//                return _impactedPerformanceIndicator;
//            }
//            private set
//            {
//                _impactedPerformanceIndicator = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ImpactedPerformanceIndicator collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ImpactedPerformanceIndicatorSpecified
//        {
//            get
//            {
//                return (this.ImpactedPerformanceIndicator.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _decisionMaker;
        
//        [System.Xml.Serialization.XmlElementAttribute("decisionMaker", Order=8)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> DecisionMaker
//        {
//            get
//            {
//                return _decisionMaker;
//            }
//            private set
//            {
//                _decisionMaker = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DecisionMaker collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DecisionMakerSpecified
//        {
//            get
//            {
//                return (this.DecisionMaker.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _decisionOwner;
        
//        [System.Xml.Serialization.XmlElementAttribute("decisionOwner", Order=9)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> DecisionOwner
//        {
//            get
//            {
//                return _decisionOwner;
//            }
//            private set
//            {
//                _decisionOwner = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DecisionOwner collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DecisionOwnerSpecified
//        {
//            get
//            {
//                return (this.DecisionOwner.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _usingProcess;
        
//        [System.Xml.Serialization.XmlElementAttribute("usingProcess", Order=10)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> UsingProcess
//        {
//            get
//            {
//                return _usingProcess;
//            }
//            private set
//            {
//                _usingProcess = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the UsingProcess collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool UsingProcessSpecified
//        {
//            get
//            {
//                return (this.UsingProcess.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _usingTask;
        
//        [System.Xml.Serialization.XmlElementAttribute("usingTask", Order=11)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> UsingTask
//        {
//            get
//            {
//                return _usingTask;
//            }
//            private set
//            {
//                _usingTask = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the UsingTask collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool UsingTaskSpecified
//        {
//            get
//            {
//                return (this.UsingTask.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=12)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=12)]
//        public TExpression Expression { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInformationRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("informationRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TInformationRequirement : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("requiredDecision", Order=0)]
//        public TdmnElementReference RequiredDecision { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("requiredInput", Order=1)]
//        public TdmnElementReference RequiredInput { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tKnowledgeRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("knowledgeRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TKnowledgeRequirement : TdmnElement
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("requiredKnowledge", Order=0)]
//        public TdmnElementReference RequiredKnowledge { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tAuthorityRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("authorityRequirement", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TAuthorityRequirement : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("requiredDecision", Order=0)]
//        public TdmnElementReference RequiredDecision { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("requiredInput", Order=1)]
//        public TdmnElementReference RequiredInput { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("requiredAuthority", Order=2)]
//        public TdmnElementReference RequiredAuthority { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tPerformanceIndicator", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("performanceIndicator", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TPerformanceIndicator : TBusinessContextElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _impactingDecision;
        
//        [System.Xml.Serialization.XmlElementAttribute("impactingDecision", Order=0)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> ImpactingDecision
//        {
//            get
//            {
//                return _impactingDecision;
//            }
//            private set
//            {
//                _impactingDecision = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ImpactingDecision collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ImpactingDecisionSpecified
//        {
//            get
//            {
//                return (this.ImpactingDecision.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TPerformanceIndicator" /> class.</para>
//        /// </summary>
//        public TPerformanceIndicator()
//        {
//            this._impactingDecision = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tOrganizationUnit", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("organizationUnit", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TOrganizationUnit : TBusinessContextElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _decisionMade;
        
//        [System.Xml.Serialization.XmlElementAttribute("decisionMade", Order=0)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> DecisionMade
//        {
//            get
//            {
//                return _decisionMade;
//            }
//            private set
//            {
//                _decisionMade = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DecisionMade collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DecisionMadeSpecified
//        {
//            get
//            {
//                return (this.DecisionMade.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TOrganizationUnit" /> class.</para>
//        /// </summary>
//        public TOrganizationUnit()
//        {
//            this._decisionMade = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._decisionOwned = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _decisionOwned;
        
//        [System.Xml.Serialization.XmlElementAttribute("decisionOwned", Order=1)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> DecisionOwned
//        {
//            get
//            {
//                return _decisionOwned;
//            }
//            private set
//            {
//                _decisionOwned = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the DecisionOwned collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool DecisionOwnedSpecified
//        {
//            get
//            {
//                return (this.DecisionOwned.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInvocable", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("invocable", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TBusinessKnowledgeModel))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TDecisionService))]
//    public partial class TInvocable : TdrgElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("variable", Order=0)]
//        public TInformationItem Variable { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tBusinessKnowledgeModel", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("businessKnowledgeModel", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TBusinessKnowledgeModel : TInvocable
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("encapsulatedLogic", Order=0)]
//        public TFunctionDefinition EncapsulatedLogic { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TKnowledgeRequirement> _knowledgeRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("knowledgeRequirement", Order=1)]
//        public System.Collections.ObjectModel.Collection<TKnowledgeRequirement> KnowledgeRequirement
//        {
//            get
//            {
//                return _knowledgeRequirement;
//            }
//            private set
//            {
//                _knowledgeRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the KnowledgeRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool KnowledgeRequirementSpecified
//        {
//            get
//            {
//                return (this.KnowledgeRequirement.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TBusinessKnowledgeModel" /> class.</para>
//        /// </summary>
//        public TBusinessKnowledgeModel()
//        {
//            this._knowledgeRequirement = new System.Collections.ObjectModel.Collection<TKnowledgeRequirement>();
//            this._authorityRequirement = new System.Collections.ObjectModel.Collection<TAuthorityRequirement>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TAuthorityRequirement> _authorityRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("authorityRequirement", Order=2)]
//        public System.Collections.ObjectModel.Collection<TAuthorityRequirement> AuthorityRequirement
//        {
//            get
//            {
//                return _authorityRequirement;
//            }
//            private set
//            {
//                _authorityRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AuthorityRequirementSpecified
//        {
//            get
//            {
//                return (this.AuthorityRequirement.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tFunctionDefinition", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("functionDefinition", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TFunctionDefinition : TExpression
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TInformationItem> _formalParameter;
        
//        [System.Xml.Serialization.XmlElementAttribute("formalParameter", Order=0)]
//        public System.Collections.ObjectModel.Collection<TInformationItem> FormalParameter
//        {
//            get
//            {
//                return _formalParameter;
//            }
//            private set
//            {
//                _formalParameter = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the FormalParameter collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool FormalParameterSpecified
//        {
//            get
//            {
//                return (this.FormalParameter.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TFunctionDefinition" /> class.</para>
//        /// </summary>
//        public TFunctionDefinition()
//        {
//            this._formalParameter = new System.Collections.ObjectModel.Collection<TInformationItem>();
//        }
        
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=1)]
//        public TExpression Expression { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private TFunctionKind _kind = VertexBPMN.Domain.Model.Dmn12.TFunctionKind.Feel;
        
//        [System.ComponentModel.DefaultValueAttribute(VertexBPMN.Domain.Model.Dmn12.TFunctionKind.Feel)]
//        [System.Xml.Serialization.XmlAttributeAttribute("kind")]
//        public TFunctionKind Kind
//        {
//            get
//            {
//                return _kind;
//            }
//            set
//            {
//                _kind = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tFunctionKind", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public enum TFunctionKind
//    {
        
//        [System.Xml.Serialization.XmlEnumAttribute("FEEL")]
//        Feel,
        
//        Java,
        
//        [System.Xml.Serialization.XmlEnumAttribute("ONNX")]
//        Onnx,
        
//        [System.Xml.Serialization.XmlEnumAttribute("PMML")]
//        Pmml,
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInputData", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("inputData", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TInputData : TdrgElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("variable", Order=0)]
//        public TInformationItem Variable { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tKnowledgeSource", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("knowledgeSource", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TKnowledgeSource : TdrgElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TAuthorityRequirement> _authorityRequirement;
        
//        [System.Xml.Serialization.XmlElementAttribute("authorityRequirement", Order=0)]
//        public System.Collections.ObjectModel.Collection<TAuthorityRequirement> AuthorityRequirement
//        {
//            get
//            {
//                return _authorityRequirement;
//            }
//            private set
//            {
//                _authorityRequirement = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AuthorityRequirement collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AuthorityRequirementSpecified
//        {
//            get
//            {
//                return (this.AuthorityRequirement.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TKnowledgeSource" /> class.</para>
//        /// </summary>
//        public TKnowledgeSource()
//        {
//            this._authorityRequirement = new System.Collections.ObjectModel.Collection<TAuthorityRequirement>();
//        }
        
//        [System.Xml.Serialization.XmlElementAttribute("type", Order=1)]
//        public string Type { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("owner", Order=2)]
//        public TdmnElementReference Owner { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("locationURI")]
//        public string LocationUri { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tLiteralExpression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("literalExpression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TLiteralExpression : TExpression
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("text", Order=0)]
//        public string Text { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("importedValues", Order=1)]
//        public TImportedValues ImportedValues { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("expressionLanguage")]
//        public string ExpressionLanguage { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tImportedValues", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TImportedValues : TImport
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("importedElement", Order=0)]
//        public string ImportedElement { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("expressionLanguage")]
//        public string ExpressionLanguage { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInvocation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("invocation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TInvocation : TExpression
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=0)]
//        public TExpression Expression { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TBinding> _binding;
        
//        [System.Xml.Serialization.XmlElementAttribute("binding", Order=1)]
//        public System.Collections.ObjectModel.Collection<TBinding> Binding
//        {
//            get
//            {
//                return _binding;
//            }
//            private set
//            {
//                _binding = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Binding collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool BindingSpecified
//        {
//            get
//            {
//                return (this.Binding.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TInvocation" /> class.</para>
//        /// </summary>
//        public TInvocation()
//        {
//            this._binding = new System.Collections.ObjectModel.Collection<TBinding>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tBinding", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TBinding
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("parameter", Order=0)]
//        public TInformationItem Parameter { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=1)]
//        public TExpression Expression { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDecisionTable", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("decisionTable", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TDecisionTable : TExpression
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TInputClause> _input;
        
//        [System.Xml.Serialization.XmlElementAttribute("input", Order=0)]
//        public System.Collections.ObjectModel.Collection<TInputClause> Input
//        {
//            get
//            {
//                return _input;
//            }
//            private set
//            {
//                _input = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Input collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool InputSpecified
//        {
//            get
//            {
//                return (this.Input.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionTable" /> class.</para>
//        /// </summary>
//        public TDecisionTable()
//        {
//            this._input = new System.Collections.ObjectModel.Collection<TInputClause>();
//            this._output = new System.Collections.ObjectModel.Collection<TOutputClause>();
//            this._annotation = new System.Collections.ObjectModel.Collection<TRuleAnnotationClause>();
//            this._rule = new System.Collections.ObjectModel.Collection<TDecisionRule>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TOutputClause> _output;
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("output", Order=1)]
//        public System.Collections.ObjectModel.Collection<TOutputClause> Output
//        {
//            get
//            {
//                return _output;
//            }
//            private set
//            {
//                _output = value;
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TRuleAnnotationClause> _annotation;
        
//        [System.Xml.Serialization.XmlElementAttribute("annotation", Order=2)]
//        public System.Collections.ObjectModel.Collection<TRuleAnnotationClause> Annotation
//        {
//            get
//            {
//                return _annotation;
//            }
//            private set
//            {
//                _annotation = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Annotation collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnnotationSpecified
//        {
//            get
//            {
//                return (this.Annotation.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TDecisionRule> _rule;
        
//        [System.Xml.Serialization.XmlElementAttribute("rule", Order=3)]
//        public System.Collections.ObjectModel.Collection<TDecisionRule> Rule
//        {
//            get
//            {
//                return _rule;
//            }
//            private set
//            {
//                _rule = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Rule collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool RuleSpecified
//        {
//            get
//            {
//                return (this.Rule.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private THitPolicy _hitPolicy = VertexBPMN.Domain.Model.Dmn12.THitPolicy.Unique;
        
//        [System.ComponentModel.DefaultValueAttribute(VertexBPMN.Domain.Model.Dmn12.THitPolicy.Unique)]
//        [System.Xml.Serialization.XmlAttributeAttribute("hitPolicy")]
//        public THitPolicy HitPolicy
//        {
//            get
//            {
//                return _hitPolicy;
//            }
//            set
//            {
//                _hitPolicy = value;
//            }
//        }
        
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        [System.Xml.Serialization.XmlAttributeAttribute("aggregation")]
//        public TBuiltinAggregator AggregationValue { get; set; }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets or sets a value indicating whether the Aggregation property is specified.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
//        public bool AggregationValueSpecified { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public System.Nullable<TBuiltinAggregator> Aggregation
//        {
//            get
//            {
//                if (this.AggregationValueSpecified)
//                {
//                    return this.AggregationValue;
//                }
//                else
//                {
//                    return null;
//                }
//            }
//            set
//            {
//                this.AggregationValue = value.GetValueOrDefault();
//                this.AggregationValueSpecified = value.HasValue;
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private TDecisionTableOrientation _preferredOrientation = VertexBPMN.Domain.Model.Dmn12.TDecisionTableOrientation.RuleAsRow;
        
//        [System.ComponentModel.DefaultValueAttribute(VertexBPMN.Domain.Model.Dmn12.TDecisionTableOrientation.RuleAsRow)]
//        [System.Xml.Serialization.XmlAttributeAttribute("preferredOrientation")]
//        public TDecisionTableOrientation PreferredOrientation
//        {
//            get
//            {
//                return _preferredOrientation;
//            }
//            set
//            {
//                _preferredOrientation = value;
//            }
//        }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("outputLabel")]
//        public string OutputLabel { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tInputClause", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TInputClause : TdmnElement
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("inputExpression", Order=0)]
//        public TLiteralExpression InputExpression { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("inputValues", Order=1)]
//        public TUnaryTests InputValues { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tOutputClause", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TOutputClause : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("outputValues", Order=0)]
//        public TUnaryTests OutputValues { get; set; }
        
//        [System.Xml.Serialization.XmlElementAttribute("defaultOutputEntry", Order=1)]
//        public TLiteralExpression DefaultOutputEntry { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("name")]
//        public string Name { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("typeRef")]
//        public string TypeRef { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tRuleAnnotationClause", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TRuleAnnotationClause
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("name")]
//        public string Name { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDecisionRule", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TDecisionRule : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TUnaryTests> _inputEntry;
        
//        [System.Xml.Serialization.XmlElementAttribute("inputEntry", Order=0)]
//        public System.Collections.ObjectModel.Collection<TUnaryTests> InputEntry
//        {
//            get
//            {
//                return _inputEntry;
//            }
//            private set
//            {
//                _inputEntry = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the InputEntry collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool InputEntrySpecified
//        {
//            get
//            {
//                return (this.InputEntry.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionRule" /> class.</para>
//        /// </summary>
//        public TDecisionRule()
//        {
//            this._inputEntry = new System.Collections.ObjectModel.Collection<TUnaryTests>();
//            this._outputEntry = new System.Collections.ObjectModel.Collection<TLiteralExpression>();
//            this._annotationEntry = new System.Collections.ObjectModel.Collection<TRuleAnnotation>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TLiteralExpression> _outputEntry;
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("outputEntry", Order=1)]
//        public System.Collections.ObjectModel.Collection<TLiteralExpression> OutputEntry
//        {
//            get
//            {
//                return _outputEntry;
//            }
//            private set
//            {
//                _outputEntry = value;
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TRuleAnnotation> _annotationEntry;
        
//        [System.Xml.Serialization.XmlElementAttribute("annotationEntry", Order=2)]
//        public System.Collections.ObjectModel.Collection<TRuleAnnotation> AnnotationEntry
//        {
//            get
//            {
//                return _annotationEntry;
//            }
//            private set
//            {
//                _annotationEntry = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the AnnotationEntry collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool AnnotationEntrySpecified
//        {
//            get
//            {
//                return (this.AnnotationEntry.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tRuleAnnotation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TRuleAnnotation
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("text", Order=0)]
//        public string Text { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tHitPolicy", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public enum THitPolicy
//    {
        
//        [System.Xml.Serialization.XmlEnumAttribute("UNIQUE")]
//        Unique,
        
//        [System.Xml.Serialization.XmlEnumAttribute("FIRST")]
//        First,
        
//        [System.Xml.Serialization.XmlEnumAttribute("PRIORITY")]
//        Priority,
        
//        [System.Xml.Serialization.XmlEnumAttribute("ANY")]
//        Any,
        
//        [System.Xml.Serialization.XmlEnumAttribute("COLLECT")]
//        Collect,
        
//        [System.Xml.Serialization.XmlEnumAttribute("RULE ORDER")]
//        RuleOrder,
        
//        [System.Xml.Serialization.XmlEnumAttribute("OUTPUT ORDER")]
//        OutputOrder,
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tBuiltinAggregator", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public enum TBuiltinAggregator
//    {
        
//        [System.Xml.Serialization.XmlEnumAttribute("SUM")]
//        Sum,
        
//        [System.Xml.Serialization.XmlEnumAttribute("COUNT")]
//        Count,
        
//        [System.Xml.Serialization.XmlEnumAttribute("MIN")]
//        Min,
        
//        [System.Xml.Serialization.XmlEnumAttribute("MAX")]
//        Max,
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDecisionTableOrientation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public enum TDecisionTableOrientation
//    {
        
//        [System.Xml.Serialization.XmlEnumAttribute("Rule-as-Row")]
//        RuleAsRow,
        
//        [System.Xml.Serialization.XmlEnumAttribute("Rule-as-Column")]
//        RuleAsColumn,
        
//        CrossTable,
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tGroup", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("group", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TGroup : TArtifact
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("name")]
//        public string Name { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tTextAnnotation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("textAnnotation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TTextAnnotation : TArtifact
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("text", Order=0)]
//        public string Text { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private string _textFormat = "text/plain";
        
//        [System.ComponentModel.DefaultValueAttribute("text/plain")]
//        [System.Xml.Serialization.XmlAttributeAttribute("textFormat")]
//        public string TextFormat
//        {
//            get
//            {
//                return _textFormat;
//            }
//            set
//            {
//                _textFormat = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tAssociation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("association", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TAssociation : TArtifact
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("sourceRef", Order=0)]
//        public TdmnElementReference SourceRef { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("targetRef", Order=1)]
//        public TdmnElementReference TargetRef { get; set; }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private TAssociationDirection _associationDirection = VertexBPMN.Domain.Model.Dmn12.TAssociationDirection.None;
        
//        [System.ComponentModel.DefaultValueAttribute(VertexBPMN.Domain.Model.Dmn12.TAssociationDirection.None)]
//        [System.Xml.Serialization.XmlAttributeAttribute("associationDirection")]
//        public TAssociationDirection AssociationDirection
//        {
//            get
//            {
//                return _associationDirection;
//            }
//            set
//            {
//                _associationDirection = value;
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tAssociationDirection", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public enum TAssociationDirection
//    {
        
//        None,
        
//        One,
        
//        Both,
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tContext", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("context", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TContext : TExpression
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TContextEntry> _contextEntry;
        
//        [System.Xml.Serialization.XmlElementAttribute("contextEntry", Order=0)]
//        public System.Collections.ObjectModel.Collection<TContextEntry> ContextEntry
//        {
//            get
//            {
//                return _contextEntry;
//            }
//            private set
//            {
//                _contextEntry = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the ContextEntry collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ContextEntrySpecified
//        {
//            get
//            {
//                return (this.ContextEntry.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TContext" /> class.</para>
//        /// </summary>
//        public TContext()
//        {
//            this._contextEntry = new System.Collections.ObjectModel.Collection<TContextEntry>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tContextEntry", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("contextEntry", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TContextEntry : TdmnElement
//    {
        
//        [System.Xml.Serialization.XmlElementAttribute("variable", Order=0)]
//        public TInformationItem Variable { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=1)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=1)]
//        public TExpression Expression { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tRelation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("relation", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TRelation : TExpression
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TInformationItem> _column;
        
//        [System.Xml.Serialization.XmlElementAttribute("column", Order=0)]
//        public System.Collections.ObjectModel.Collection<TInformationItem> Column
//        {
//            get
//            {
//                return _column;
//            }
//            private set
//            {
//                _column = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Column collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ColumnSpecified
//        {
//            get
//            {
//                return (this.Column.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TRelation" /> class.</para>
//        /// </summary>
//        public TRelation()
//        {
//            this._column = new System.Collections.ObjectModel.Collection<TInformationItem>();
//            this._row = new System.Collections.ObjectModel.Collection<TList>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TList> _row;
        
//        [System.Xml.Serialization.XmlElementAttribute("row", Order=1)]
//        public System.Collections.ObjectModel.Collection<TList> Row
//        {
//            get
//            {
//                return _row;
//            }
//            private set
//            {
//                _row = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Row collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool RowSpecified
//        {
//            get
//            {
//                return (this.Row.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tList", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("list", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TList : TExpression
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TExpression> _expression;
        
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=0)]
//        public System.Collections.ObjectModel.Collection<TExpression> Expression
//        {
//            get
//            {
//                return _expression;
//            }
//            private set
//            {
//                _expression = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the Expression collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool ExpressionSpecified
//        {
//            get
//            {
//                return (this.Expression.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TList" /> class.</para>
//        /// </summary>
//        public TList()
//        {
//            this._expression = new System.Collections.ObjectModel.Collection<TExpression>();
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tDecisionService", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("decisionService", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TDecisionService : TInvocable
//    {
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _outputDecision;
        
//        [System.Xml.Serialization.XmlElementAttribute("outputDecision", Order=0)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> OutputDecision
//        {
//            get
//            {
//                return _outputDecision;
//            }
//            private set
//            {
//                _outputDecision = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the OutputDecision collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool OutputDecisionSpecified
//        {
//            get
//            {
//                return (this.OutputDecision.Count != 0);
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Initializes a new instance of the <see cref="TDecisionService" /> class.</para>
//        /// </summary>
//        public TDecisionService()
//        {
//            this._outputDecision = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._encapsulatedDecision = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._inputDecision = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//            this._inputData = new System.Collections.ObjectModel.Collection<TdmnElementReference>();
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _encapsulatedDecision;
        
//        [System.Xml.Serialization.XmlElementAttribute("encapsulatedDecision", Order=1)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> EncapsulatedDecision
//        {
//            get
//            {
//                return _encapsulatedDecision;
//            }
//            private set
//            {
//                _encapsulatedDecision = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the EncapsulatedDecision collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool EncapsulatedDecisionSpecified
//        {
//            get
//            {
//                return (this.EncapsulatedDecision.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _inputDecision;
        
//        [System.Xml.Serialization.XmlElementAttribute("inputDecision", Order=2)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> InputDecision
//        {
//            get
//            {
//                return _inputDecision;
//            }
//            private set
//            {
//                _inputDecision = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the InputDecision collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool InputDecisionSpecified
//        {
//            get
//            {
//                return (this.InputDecision.Count != 0);
//            }
//        }
        
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        private System.Collections.ObjectModel.Collection<TdmnElementReference> _inputData;
        
//        [System.Xml.Serialization.XmlElementAttribute("inputData", Order=3)]
//        public System.Collections.ObjectModel.Collection<TdmnElementReference> InputData
//        {
//            get
//            {
//                return _inputData;
//            }
//            private set
//            {
//                _inputData = value;
//            }
//        }
        
//        /// <summary>
//        /// <para xml:lang="en">Gets a value indicating whether the InputData collection is empty.</para>
//        /// </summary>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool InputDataSpecified
//        {
//            get
//            {
//                return (this.InputData.Count != 0);
//            }
//        }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tChildExpression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TTypedChildExpression))]
//    public partial class TChildExpression
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("literalExpression", Type=typeof(TLiteralExpression), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("invocation", Type=typeof(TInvocation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("decisionTable", Type=typeof(TDecisionTable), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("context", Type=typeof(TContext), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("functionDefinition", Type=typeof(TFunctionDefinition), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("relation", Type=typeof(TRelation), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("list", Type=typeof(TList), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("for", Type=typeof(TFor), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("every", Type=typeof(Every), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("some", Type=typeof(Some), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("conditional", Type=typeof(TConditional), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("filter", Type=typeof(TFilter), Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/", Order=0)]
//        [System.Xml.Serialization.XmlElementAttribute("expression", Order=0)]
//        public TExpression Expression { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("id")]
//        public string Id { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tTypedChildExpression", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    public partial class TTypedChildExpression : TChildExpression
//    {
        
//        [System.Xml.Serialization.XmlAttributeAttribute("typeRef")]
//        public string TypeRef { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tIterator", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Every))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Some))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TFor))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(TQuantified))]
//    public partial class TIterator : TExpression
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("in", Order=0)]
//        public TTypedChildExpression In { get; set; }
        
//        [System.Xml.Serialization.XmlAttributeAttribute("iteratorVariable")]
//        public string IteratorVariable { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tFor", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("for", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TFor : TIterator
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("return", Order=0)]
//        public TChildExpression Return { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tQuantified", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Every))]
//    [System.Xml.Serialization.XmlIncludeAttribute(typeof(Some))]
//    public partial class TQuantified : TIterator
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("satisfies", Order=0)]
//        public TChildExpression Satisfies { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tConditional", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("conditional", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TConditional : TExpression
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("if", Order=0)]
//        public TChildExpression If { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("then", Order=1)]
//        public TChildExpression Then { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("else", Order=2)]
//        public TChildExpression Else { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("tFilter", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("filter", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class TFilter : TExpression
//    {
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("in", Order=0)]
//        public TChildExpression In { get; set; }
        
//        [System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings=true)]
//        [System.Xml.Serialization.XmlElementAttribute("match", Order=1)]
//        public TChildExpression Match { get; set; }
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("some", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("some", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class Some : TQuantified
//    {
//    }
    
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("XmlSchemaClassGenerator", "3.0.1188.0")]
//    [System.SerializableAttribute()]
//    [System.Xml.Serialization.XmlTypeAttribute("every", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlRootAttribute("every", Namespace="https://www.omg.org/spec/DMN/20240513/MODEL/")]
//    public partial class Every : TQuantified
//    {
//    }
//}
