namespace VertexBPMN.Domain.Model.Bpmn
{
    using System;
    using System.Linq;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Xml.Serialization;
    using System.Xml;
    using System.Diagnostics;
    using System.CodeDom.Compiler;
    using System.ComponentModel.DataAnnotations;
    using System.Collections.ObjectModel;


    [Serializable]
    [XmlType("Font", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Font", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    public partial class Font : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _size;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("size")]
        public double SizeValue
        {
            get
            {
                return _size;
            }
            set
            {
                if (!_size.Equals(value))
                {
                _size = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Size property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool SizeValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<double> Size
        {
            get
            {
                if (this.SizeValueSpecified)
                {
                    return this.SizeValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.SizeValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.SizeValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.SizeValue = value.GetValueOrDefault();
                    this.SizeValueSpecified = value.HasValue;
                    OnPropertyChanged("Size");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isBold;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isBold")]
        public bool IsBoldValue
        {
            get
            {
                return _isBold;
            }
            set
            {
                if (!_isBold.Equals(value))
                {
                _isBold = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsBold property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsBoldValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsBold
        {
            get
            {
                if (this.IsBoldValueSpecified)
                {
                    return this.IsBoldValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsBoldValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsBoldValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsBoldValue = value.GetValueOrDefault();
                    this.IsBoldValueSpecified = value.HasValue;
                    OnPropertyChanged("IsBold");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isItalic;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isItalic")]
        public bool IsItalicValue
        {
            get
            {
                return _isItalic;
            }
            set
            {
                if (!_isItalic.Equals(value))
                {
                _isItalic = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsItalic property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsItalicValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsItalic
        {
            get
            {
                if (this.IsItalicValueSpecified)
                {
                    return this.IsItalicValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsItalicValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsItalicValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsItalicValue = value.GetValueOrDefault();
                    this.IsItalicValueSpecified = value.HasValue;
                    OnPropertyChanged("IsItalic");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isUnderline;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isUnderline")]
        public bool IsUnderlineValue
        {
            get
            {
                return _isUnderline;
            }
            set
            {
                if (!_isUnderline.Equals(value))
                {
                _isUnderline = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsUnderline property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsUnderlineValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsUnderline
        {
            get
            {
                if (this.IsUnderlineValueSpecified)
                {
                    return this.IsUnderlineValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsUnderlineValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsUnderlineValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsUnderlineValue = value.GetValueOrDefault();
                    this.IsUnderlineValueSpecified = value.HasValue;
                    OnPropertyChanged("IsUnderline");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isStrikeThrough;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isStrikeThrough")]
        public bool IsStrikeThroughValue
        {
            get
            {
                return _isStrikeThrough;
            }
            set
            {
                if (!_isStrikeThrough.Equals(value))
                {
                _isStrikeThrough = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsStrikeThrough property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsStrikeThroughValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsStrikeThrough
        {
            get
            {
                if (this.IsStrikeThroughValueSpecified)
                {
                    return this.IsStrikeThroughValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsStrikeThroughValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsStrikeThroughValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsStrikeThroughValue = value.GetValueOrDefault();
                    this.IsStrikeThroughValueSpecified = value.HasValue;
                    OnPropertyChanged("IsStrikeThrough");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("Point", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Point", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    public partial class Point : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private double _x;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("x")]
        public double X
        {
            get
            {
                return _x;
            }
            set
            {
                if (!_x.Equals(value))
                {
                _x = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _y;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("y")]
        public double Y
        {
            get
            {
                return _y;
            }
            set
            {
                if (!_y.Equals(value))
                {
                _y = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("Bounds", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Bounds", Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    public partial class Bounds : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private double _x;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("x")]
        public double X
        {
            get
            {
                return _x;
            }
            set
            {
                if (!_x.Equals(value))
                {
                _x = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _y;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("y")]
        public double Y
        {
            get
            {
                return _y;
            }
            set
            {
                if (!_y.Equals(value))
                {
                _y = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _width;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("width")]
        public double Width
        {
            get
            {
                return _width;
            }
            set
            {
                if (!_width.Equals(value))
                {
                _width = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _height;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("height")]
        public double Height
        {
            get
            {
                return _height;
            }
            set
            {
                if (!_height.Equals(value))
                {
                _height = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("DiagramElement", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("DiagramElement", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
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
    public abstract partial class DiagramElement : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private DiagramElementExtension _extension;
        
        [XmlElement("extension", Order=0)]
        public DiagramElementExtension Extension
        {
            get
            {
                return _extension;
            }
            set
            {
                if (_extension == value)
                    return;
                if (_extension == null || value == null || !_extension.Equals(value))
                {
                _extension = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlAttribute> _anyAttribute;
        
        [XmlAnyAttributeAttribute]
        public List<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            set
            {
                if (_anyAttribute == value)
                    return;
                if (_anyAttribute == null || value == null || !_anyAttribute.SequenceEqual(value))
                {
                _anyAttribute = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AnyAttributeSpecified
        {
            get
            {
                return (this.AnyAttribute.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="DiagramElement" /> class.</para>
        /// </summary>
        public DiagramElement()
        {
            this._anyAttribute = new List<XmlAttribute>();
        }
    }
    
    
    [Serializable]
    [XmlType("DiagramElementExtension", Namespace="http://www.omg.org/spec/DD/20100524/DI", AnonymousType=true)]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    public partial class DiagramElementExtension : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<XmlElement> _any;
        
        [XmlAnyElementAttribute(Order=0)]
        public List<XmlElement> Any
        {
            get
            {
                return _any;
            }
            set
            {
                if (_any == value)
                    return;
                if (_any == null || value == null || !_any.SequenceEqual(value))
                {
                _any = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
        /// </summary>
        [XmlIgnore]
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
            this._any = new List<XmlElement>();
        }
    }
    
    
    [Serializable]
    [XmlType("Diagram", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Diagram", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnDiagram))]
    public abstract partial class Diagram : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _documentation;
        
        [XmlAttribute("documentation")]
        public string Documentation
        {
            get
            {
                return _documentation;
            }
            set
            {
                if (_documentation == value)
                    return;
                if (_documentation == null || value == null || !_documentation.Equals(value))
                {
                _documentation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private double _resolution;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("resolution")]
        public double ResolutionValue
        {
            get
            {
                return _resolution;
            }
            set
            {
                if (!_resolution.Equals(value))
                {
                _resolution = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Resolution property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ResolutionValueSpecified { get; set; }
        
        [XmlIgnore]
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
                if (((this.ResolutionValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.ResolutionValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.ResolutionValue = value.GetValueOrDefault();
                    this.ResolutionValueSpecified = value.HasValue;
                    OnPropertyChanged("Resolution");
                }
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("Node", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Node", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnLabel))]
    [XmlInclude(typeof(BpmnPlane))]
    [XmlInclude(typeof(BpmnShape))]
    [XmlInclude(typeof(Label))]
    [XmlInclude(typeof(LabeledShape))]
    [XmlInclude(typeof(Plane))]
    [XmlInclude(typeof(Shape))]
    public abstract partial class Node : DiagramElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("Edge", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Edge", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnEdge))]
    [XmlInclude(typeof(LabeledEdge))]
    public abstract partial class Edge : DiagramElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Point> _waypoint;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("waypoint", Order=0)]
        public List<Point> Waypoint
        {
            get
            {
                return _waypoint;
            }
            set
            {
                if (_waypoint == value)
                    return;
                if (_waypoint == null || value == null || !_waypoint.SequenceEqual(value))
                {
                _waypoint = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Edge" /> class.</para>
        /// </summary>
        public Edge()
        {
            this._waypoint = new List<Point>();
        }
    }
    
    
    [Serializable]
    [XmlType("LabeledEdge", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("LabeledEdge", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnEdge))]
    public abstract partial class LabeledEdge : Edge, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("Shape", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Shape", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnShape))]
    [XmlInclude(typeof(LabeledShape))]
    public abstract partial class Shape : Node, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Bounds _bounds;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("Bounds", Order=0, Namespace="http://www.omg.org/spec/DD/20100524/DC")]
        public Bounds Bounds
        {
            get
            {
                return _bounds;
            }
            set
            {
                if (_bounds == value)
                    return;
                if (_bounds == null || value == null || !_bounds.Equals(value))
                {
                _bounds = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("LabeledShape", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("LabeledShape", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnShape))]
    public abstract partial class LabeledShape : Shape, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("Label", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Label", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnLabel))]
    public abstract partial class Label : Node, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Bounds _bounds;
        
        [XmlElement("Bounds", Order=0, Namespace="http://www.omg.org/spec/DD/20100524/DC")]
        public Bounds Bounds
        {
            get
            {
                return _bounds;
            }
            set
            {
                if (_bounds == value)
                    return;
                if (_bounds == null || value == null || !_bounds.Equals(value))
                {
                _bounds = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("Plane", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Plane", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnPlane))]
    public abstract partial class Plane : Node, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<DiagramElement> _diagramElement;
        
        [XmlElement("BPMNShape", Type=typeof(BpmnShape), Namespace="http://www.omg.org/spec/BPMN/20100524/DI", Order=0)]
        [XmlElement("BPMNEdge", Type=typeof(BpmnEdge), Namespace="http://www.omg.org/spec/BPMN/20100524/DI", Order=0)]
        [XmlElement("DiagramElement", Order=0)]
        public List<DiagramElement> DiagramElement
        {
            get
            {
                return _diagramElement;
            }
            set
            {
                if (_diagramElement == value)
                    return;
                if (_diagramElement == null || value == null || !_diagramElement.SequenceEqual(value))
                {
                _diagramElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DiagramElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DiagramElementSpecified
        {
            get
            {
                return (this.DiagramElement.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Plane" /> class.</para>
        /// </summary>
        public Plane()
        {
            this._diagramElement = new List<DiagramElement>();
        }
    }
    
    
    [Serializable]
    [XmlType("Style", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("Style", Namespace="http://www.omg.org/spec/DD/20100524/DI")]
    [XmlInclude(typeof(BpmnLabelStyle))]
    public abstract partial class Style : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("BPMNDiagram", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNDiagram", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnDiagram : Diagram, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private BpmnPlane _bpmnPlane;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("BPMNPlane", Order=0)]
        public BpmnPlane BpmnPlane
        {
            get
            {
                return _bpmnPlane;
            }
            set
            {
                if (_bpmnPlane == value)
                    return;
                if (_bpmnPlane == null || value == null || !_bpmnPlane.Equals(value))
                {
                _bpmnPlane = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<BpmnLabelStyle> _bpmnLabelStyle;
        
        [XmlElement("BPMNLabelStyle", Order=1)]
        public List<BpmnLabelStyle> BpmnLabelStyle
        {
            get
            {
                return _bpmnLabelStyle;
            }
            set
            {
                if (_bpmnLabelStyle == value)
                    return;
                if (_bpmnLabelStyle == null || value == null || !_bpmnLabelStyle.SequenceEqual(value))
                {
                _bpmnLabelStyle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the BpmnLabelStyle collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool BpmnLabelStyleSpecified
        {
            get
            {
                return (this.BpmnLabelStyle.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="BpmnDiagram" /> class.</para>
        /// </summary>
        public BpmnDiagram()
        {
            this._bpmnLabelStyle = new List<BpmnLabelStyle>();
        }
    }
    
    
    [Serializable]
    [XmlType("BPMNPlane", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNPlane", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnPlane : Plane, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _bpmnElement;
        
        [XmlAttribute("bpmnElement")]
        public XmlQualifiedName BpmnElement
        {
            get
            {
                return _bpmnElement;
            }
            set
            {
                if (_bpmnElement == value)
                    return;
                if (_bpmnElement == null || value == null || !_bpmnElement.Equals(value))
                {
                _bpmnElement = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("BPMNLabelStyle", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNLabelStyle", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnLabelStyle : Style, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Font _font;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("Font", Order=0, Namespace="http://www.omg.org/spec/DD/20100524/DC")]
        public Font Font
        {
            get
            {
                return _font;
            }
            set
            {
                if (_font == value)
                    return;
                if (_font == null || value == null || !_font.Equals(value))
                {
                _font = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("BPMNEdge", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNEdge", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnEdge : LabeledEdge, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private BpmnLabel _bpmnLabel;
        
        [XmlElement("BPMNLabel", Order=0)]
        public BpmnLabel BpmnLabel
        {
            get
            {
                return _bpmnLabel;
            }
            set
            {
                if (_bpmnLabel == value)
                    return;
                if (_bpmnLabel == null || value == null || !_bpmnLabel.Equals(value))
                {
                _bpmnLabel = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _bpmnElement;
        
        [XmlAttribute("bpmnElement")]
        public XmlQualifiedName BpmnElement
        {
            get
            {
                return _bpmnElement;
            }
            set
            {
                if (_bpmnElement == value)
                    return;
                if (_bpmnElement == null || value == null || !_bpmnElement.Equals(value))
                {
                _bpmnElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _sourceElement;
        
        [XmlAttribute("sourceElement")]
        public XmlQualifiedName SourceElement
        {
            get
            {
                return _sourceElement;
            }
            set
            {
                if (_sourceElement == value)
                    return;
                if (_sourceElement == null || value == null || !_sourceElement.Equals(value))
                {
                _sourceElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _targetElement;
        
        [XmlAttribute("targetElement")]
        public XmlQualifiedName TargetElement
        {
            get
            {
                return _targetElement;
            }
            set
            {
                if (_targetElement == value)
                    return;
                if (_targetElement == null || value == null || !_targetElement.Equals(value))
                {
                _targetElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private MessageVisibleKind _messageVisibleKind;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("messageVisibleKind")]
        public MessageVisibleKind MessageVisibleKindValue
        {
            get
            {
                return _messageVisibleKind;
            }
            set
            {
                if (!_messageVisibleKind.Equals(value))
                {
                _messageVisibleKind = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the MessageVisibleKind property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool MessageVisibleKindValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<MessageVisibleKind> MessageVisibleKind
        {
            get
            {
                if (this.MessageVisibleKindValueSpecified)
                {
                    return this.MessageVisibleKindValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.MessageVisibleKindValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.MessageVisibleKindValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.MessageVisibleKindValue = value.GetValueOrDefault();
                    this.MessageVisibleKindValueSpecified = value.HasValue;
                    OnPropertyChanged("MessageVisibleKind");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("BPMNLabel", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNLabel", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnLabel : Label, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _labelStyle;
        
        [XmlAttribute("labelStyle")]
        public XmlQualifiedName LabelStyle
        {
            get
            {
                return _labelStyle;
            }
            set
            {
                if (_labelStyle == value)
                    return;
                if (_labelStyle == null || value == null || !_labelStyle.Equals(value))
                {
                _labelStyle = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("MessageVisibleKind", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public enum MessageVisibleKind
    {
        
        [XmlEnumAttribute("initiating")]
        Initiating,
        
        [XmlEnumAttribute("non_initiating")]
        NonInitiating,
    }
    
    
    [Serializable]
    [XmlType("BPMNShape", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("BPMNShape", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public partial class BpmnShape : LabeledShape, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private BpmnLabel _bpmnLabel;
        
        [XmlElement("BPMNLabel", Order=0)]
        public BpmnLabel BpmnLabel
        {
            get
            {
                return _bpmnLabel;
            }
            set
            {
                if (_bpmnLabel == value)
                    return;
                if (_bpmnLabel == null || value == null || !_bpmnLabel.Equals(value))
                {
                _bpmnLabel = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _bpmnElement;
        
        [XmlAttribute("bpmnElement")]
        public XmlQualifiedName BpmnElement
        {
            get
            {
                return _bpmnElement;
            }
            set
            {
                if (_bpmnElement == value)
                    return;
                if (_bpmnElement == null || value == null || !_bpmnElement.Equals(value))
                {
                _bpmnElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isHorizontal;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isHorizontal")]
        public bool IsHorizontalValue
        {
            get
            {
                return _isHorizontal;
            }
            set
            {
                if (!_isHorizontal.Equals(value))
                {
                _isHorizontal = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsHorizontal property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsHorizontalValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsHorizontal
        {
            get
            {
                if (this.IsHorizontalValueSpecified)
                {
                    return this.IsHorizontalValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsHorizontalValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsHorizontalValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsHorizontalValue = value.GetValueOrDefault();
                    this.IsHorizontalValueSpecified = value.HasValue;
                    OnPropertyChanged("IsHorizontal");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isExpanded;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isExpanded")]
        public bool IsExpandedValue
        {
            get
            {
                return _isExpanded;
            }
            set
            {
                if (!_isExpanded.Equals(value))
                {
                _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsExpanded property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsExpandedValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsExpanded
        {
            get
            {
                if (this.IsExpandedValueSpecified)
                {
                    return this.IsExpandedValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsExpandedValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsExpandedValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsExpandedValue = value.GetValueOrDefault();
                    this.IsExpandedValueSpecified = value.HasValue;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isMarkerVisible;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isMarkerVisible")]
        public bool IsMarkerVisibleValue
        {
            get
            {
                return _isMarkerVisible;
            }
            set
            {
                if (!_isMarkerVisible.Equals(value))
                {
                _isMarkerVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsMarkerVisible property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsMarkerVisibleValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsMarkerVisible
        {
            get
            {
                if (this.IsMarkerVisibleValueSpecified)
                {
                    return this.IsMarkerVisibleValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsMarkerVisibleValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsMarkerVisibleValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsMarkerVisibleValue = value.GetValueOrDefault();
                    this.IsMarkerVisibleValueSpecified = value.HasValue;
                    OnPropertyChanged("IsMarkerVisible");
                }
            }
        }
        
        [XmlIgnore]
        private bool _isMessageVisible;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isMessageVisible")]
        public bool IsMessageVisibleValue
        {
            get
            {
                return _isMessageVisible;
            }
            set
            {
                if (!_isMessageVisible.Equals(value))
                {
                _isMessageVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsMessageVisible property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsMessageVisibleValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsMessageVisible
        {
            get
            {
                if (this.IsMessageVisibleValueSpecified)
                {
                    return this.IsMessageVisibleValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsMessageVisibleValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsMessageVisibleValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsMessageVisibleValue = value.GetValueOrDefault();
                    this.IsMessageVisibleValueSpecified = value.HasValue;
                    OnPropertyChanged("IsMessageVisible");
                }
            }
        }
        
        [XmlIgnore]
        private ParticipantBandKind _participantBandKind;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("participantBandKind")]
        public ParticipantBandKind ParticipantBandKindValue
        {
            get
            {
                return _participantBandKind;
            }
            set
            {
                if (!_participantBandKind.Equals(value))
                {
                _participantBandKind = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the ParticipantBandKind property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ParticipantBandKindValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<ParticipantBandKind> ParticipantBandKind
        {
            get
            {
                if (this.ParticipantBandKindValueSpecified)
                {
                    return this.ParticipantBandKindValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.ParticipantBandKindValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.ParticipantBandKindValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.ParticipantBandKindValue = value.GetValueOrDefault();
                    this.ParticipantBandKindValueSpecified = value.HasValue;
                    OnPropertyChanged("ParticipantBandKind");
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _choreographyActivityShape;
        
        [XmlAttribute("choreographyActivityShape")]
        public XmlQualifiedName ChoreographyActivityShape
        {
            get
            {
                return _choreographyActivityShape;
            }
            set
            {
                if (_choreographyActivityShape == value)
                    return;
                if (_choreographyActivityShape == null || value == null || !_choreographyActivityShape.Equals(value))
                {
                _choreographyActivityShape = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("ParticipantBandKind", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
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
    [XmlType("tActivity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("activity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class Activity : FlowNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private InputOutputSpecification _ioSpecification;
        
        [XmlElement("ioSpecification", Order=0)]
        public InputOutputSpecification IoSpecification
        {
            get
            {
                return _ioSpecification;
            }
            set
            {
                if (_ioSpecification == value)
                    return;
                if (_ioSpecification == null || value == null || !_ioSpecification.Equals(value))
                {
                _ioSpecification = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<Property> _property;
        
        [XmlElement("property", Order=1)]
        public List<Property> Property
        {
            get
            {
                return _property;
            }
            set
            {
                if (_property == value)
                    return;
                if (_property == null || value == null || !_property.SequenceEqual(value))
                {
                _property = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Property collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool PropertySpecified
        {
            get
            {
                return (this.Property.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Activity" /> class.</para>
        /// </summary>
        public Activity()
        {
            this._property = new List<Property>();
            this._dataInputAssociation = new List<DataInputAssociation>();
            this._dataOutputAssociation = new List<DataOutputAssociation>();
            this._resourceRole = new List<ResourceRole>();
        }
        
        [XmlIgnore]
        private List<DataInputAssociation> _dataInputAssociation;
        
        [XmlElement("dataInputAssociation", Order=2)]
        public List<DataInputAssociation> DataInputAssociation
        {
            get
            {
                return _dataInputAssociation;
            }
            set
            {
                if (_dataInputAssociation == value)
                    return;
                if (_dataInputAssociation == null || value == null || !_dataInputAssociation.SequenceEqual(value))
                {
                _dataInputAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataInputAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataInputAssociationSpecified
        {
            get
            {
                return (this.DataInputAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<DataOutputAssociation> _dataOutputAssociation;
        
        [XmlElement("dataOutputAssociation", Order=3)]
        public List<DataOutputAssociation> DataOutputAssociation
        {
            get
            {
                return _dataOutputAssociation;
            }
            set
            {
                if (_dataOutputAssociation == value)
                    return;
                if (_dataOutputAssociation == null || value == null || !_dataOutputAssociation.SequenceEqual(value))
                {
                _dataOutputAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataOutputAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataOutputAssociationSpecified
        {
            get
            {
                return (this.DataOutputAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ResourceRole> _resourceRole;
        
        [XmlElement("performer", Type=typeof(Performer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("humanPerformer", Type=typeof(HumanPerformer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("potentialOwner", Type=typeof(PotentialOwner), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("resourceRole", Order=4)]
        public List<ResourceRole> ResourceRole
        {
            get
            {
                return _resourceRole;
            }
            set
            {
                if (_resourceRole == value)
                    return;
                if (_resourceRole == null || value == null || !_resourceRole.SequenceEqual(value))
                {
                _resourceRole = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ResourceRole collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ResourceRoleSpecified
        {
            get
            {
                return (this.ResourceRole.Count != 0);
            }
        }
        
        [XmlIgnore]
        private LoopCharacteristics _loopCharacteristics;
        
        [XmlElement("multiInstanceLoopCharacteristics", Type=typeof(MultiInstanceLoopCharacteristics), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=5)]
        [XmlElement("standardLoopCharacteristics", Type=typeof(StandardLoopCharacteristics), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=5)]
        [XmlElement("loopCharacteristics", Order=5)]
        public LoopCharacteristics LoopCharacteristics
        {
            get
            {
                return _loopCharacteristics;
            }
            set
            {
                if (_loopCharacteristics == value)
                    return;
                if (_loopCharacteristics == null || value == null || !_loopCharacteristics.Equals(value))
                {
                _loopCharacteristics = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isForCompensation = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isForCompensation")]
        public bool IsForCompensation
        {
            get
            {
                return _isForCompensation;
            }
            set
            {
                if (!_isForCompensation.Equals(value))
                {
                _isForCompensation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _startQuantity = "1";
        
        [DefaultValueAttribute("1")]
        [XmlAttribute("startQuantity")]
        public string StartQuantity
        {
            get
            {
                return _startQuantity;
            }
            set
            {
                if (_startQuantity == value)
                    return;
                if (_startQuantity == null || value == null || !_startQuantity.Equals(value))
                {
                _startQuantity = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _completionQuantity = "1";
        
        [DefaultValueAttribute("1")]
        [XmlAttribute("completionQuantity")]
        public string CompletionQuantity
        {
            get
            {
                return _completionQuantity;
            }
            set
            {
                if (_completionQuantity == value)
                    return;
                if (_completionQuantity == null || value == null || !_completionQuantity.Equals(value))
                {
                _completionQuantity = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _default;
        
        [XmlAttribute("default")]
        public string Default
        {
            get
            {
                return _default;
            }
            set
            {
                if (_default == value)
                    return;
                if (_default == null || value == null || !_default.Equals(value))
                {
                _default = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tFlowNode", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("flowNode", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class FlowNode : FlowElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _incoming;
        
        [XmlElement("incoming", Order=0)]
        public List<XmlQualifiedName> Incoming
        {
            get
            {
                return _incoming;
            }
            set
            {
                if (_incoming == value)
                    return;
                if (_incoming == null || value == null || !_incoming.SequenceEqual(value))
                {
                _incoming = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Incoming collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool IncomingSpecified
        {
            get
            {
                return (this.Incoming.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="FlowNode" /> class.</para>
        /// </summary>
        public FlowNode()
        {
            this._incoming = new List<XmlQualifiedName>();
            this._outgoing = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _outgoing;
        
        [XmlElement("outgoing", Order=1)]
        public List<XmlQualifiedName> Outgoing
        {
            get
            {
                return _outgoing;
            }
            set
            {
                if (_outgoing == value)
                    return;
                if (_outgoing == null || value == null || !_outgoing.SequenceEqual(value))
                {
                _outgoing = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Outgoing collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutgoingSpecified
        {
            get
            {
                return (this.Outgoing.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tFlowElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("flowElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class FlowElement : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Auditing _auditing;
        
        [XmlElement("auditing", Order=0)]
        public Auditing Auditing
        {
            get
            {
                return _auditing;
            }
            set
            {
                if (_auditing == value)
                    return;
                if (_auditing == null || value == null || !_auditing.Equals(value))
                {
                _auditing = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private Monitoring _monitoring;
        
        [XmlElement("monitoring", Order=1)]
        public Monitoring Monitoring
        {
            get
            {
                return _monitoring;
            }
            set
            {
                if (_monitoring == value)
                    return;
                if (_monitoring == null || value == null || !_monitoring.Equals(value))
                {
                _monitoring = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _categoryValueRef;
        
        [XmlElement("categoryValueRef", Order=2)]
        public List<XmlQualifiedName> CategoryValueRef
        {
            get
            {
                return _categoryValueRef;
            }
            set
            {
                if (_categoryValueRef == value)
                    return;
                if (_categoryValueRef == null || value == null || !_categoryValueRef.SequenceEqual(value))
                {
                _categoryValueRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CategoryValueRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CategoryValueRefSpecified
        {
            get
            {
                return (this.CategoryValueRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="FlowElement" /> class.</para>
        /// </summary>
        public FlowElement()
        {
            this._categoryValueRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tBaseElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("baseElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class BaseElement : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<Documentation> _documentation;
        
        [XmlElement("documentation", Order=0)]
        public List<Documentation> Documentation
        {
            get
            {
                return _documentation;
            }
            set
            {
                if (_documentation == value)
                    return;
                if (_documentation == null || value == null || !_documentation.SequenceEqual(value))
                {
                _documentation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Documentation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DocumentationSpecified
        {
            get
            {
                return (this.Documentation.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="BaseElement" /> class.</para>
        /// </summary>
        public BaseElement()
        {
            this._documentation = new List<Documentation>();
            this._anyAttribute = new List<XmlAttribute>();
        }
        
        [XmlIgnore]
        private ExtensionElements _extensionElements;
        
        [XmlElement("extensionElements", Order=1)]
        public ExtensionElements ExtensionElements
        {
            get
            {
                return _extensionElements;
            }
            set
            {
                if (_extensionElements == value)
                    return;
                if (_extensionElements == null || value == null || !_extensionElements.Equals(value))
                {
                _extensionElements = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlAttribute> _anyAttribute;

        [XmlAnyAttributeAttribute]
        public List<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            set
            {
                if (_anyAttribute == value)
                    return;
                if (_anyAttribute == null || value == null || !_anyAttribute.SequenceEqual(value))
                {
                _anyAttribute = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AnyAttributeSpecified
        {
            get
            {
                return (this.AnyAttribute.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDocumentation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("documentation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Documentation : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private XmlElement _any;
        
        [XmlAnyElementAttribute(Order=0)]
        public XmlElement Any
        {
            get
            {
                return _any;
            }
            set
            {
                if (_any == value)
                    return;
                if (_any == null || value == null || !_any.Equals(value))
                {
                _any = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _textFormat = "text/plain";
        
        [DefaultValueAttribute("text/plain")]
        [XmlAttribute("textFormat")]
        public string TextFormat
        {
            get
            {
                return _textFormat;
            }
            set
            {
                if (_textFormat == value)
                    return;
                if (_textFormat == null || value == null || !_textFormat.Equals(value))
                {
                _textFormat = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlTextAttribute()]
        public string[] Text { get; set; }
    }
    
    
    [Serializable]
    [XmlType("tExtensionElements", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("extensionElements", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ExtensionElements : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<XmlElement> _any;
        
        [XmlAnyElementAttribute(Order=0)]
        public List<XmlElement> Any
        {
            get
            {
                return _any;
            }
            set
            {
                if (_any == value)
                    return;
                if (_any == null || value == null || !_any.SequenceEqual(value))
                {
                _any = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Any collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ExtensionElements" /> class.</para>
        /// </summary>
        public ExtensionElements()
        {
            this._any = new List<XmlElement>();
        }
    }
    
    
    [Serializable]
    [XmlType("tAuditing", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("auditing", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Auditing : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tMonitoring", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("monitoring", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Monitoring : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tInputOutputSpecification", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("ioSpecification", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class InputOutputSpecification : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<DataInput> _dataInput;
        
        [XmlElement("dataInput", Order=0)]
        public List<DataInput> DataInput
        {
            get
            {
                return _dataInput;
            }
            set
            {
                if (_dataInput == value)
                    return;
                if (_dataInput == null || value == null || !_dataInput.SequenceEqual(value))
                {
                _dataInput = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataInput collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataInputSpecified
        {
            get
            {
                return (this.DataInput.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="InputOutputSpecification" /> class.</para>
        /// </summary>
        public InputOutputSpecification()
        {
            this._dataInput = new List<DataInput>();
            this._dataOutput = new List<DataOutput>();
            this._inputSet = new List<InputSet>();
            this._outputSet = new List<OutputSet>();
        }
        
        [XmlIgnore]
        private List<DataOutput> _dataOutput;
        
        [XmlElement("dataOutput", Order=1)]
        public List<DataOutput> DataOutput
        {
            get
            {
                return _dataOutput;
            }
            set
            {
                if (_dataOutput == value)
                    return;
                if (_dataOutput == null || value == null || !_dataOutput.SequenceEqual(value))
                {
                _dataOutput = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataOutput collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataOutputSpecified
        {
            get
            {
                return (this.DataOutput.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<InputSet> _inputSet;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("inputSet", Order=2)]
        public List<InputSet> InputSet
        {
            get
            {
                return _inputSet;
            }
            set
            {
                if (_inputSet == value)
                    return;
                if (_inputSet == null || value == null || !_inputSet.SequenceEqual(value))
                {
                _inputSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<OutputSet> _outputSet;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("outputSet", Order=3)]
        public List<OutputSet> OutputSet
        {
            get
            {
                return _outputSet;
            }
            set
            {
                if (_outputSet == value)
                    return;
                if (_outputSet == null || value == null || !_outputSet.SequenceEqual(value))
                {
                _outputSet = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataInput", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataInput", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataInput : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isCollection = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isCollection")]
        public bool IsCollection
        {
            get
            {
                return _isCollection;
            }
            set
            {
                if (!_isCollection.Equals(value))
                {
                _isCollection = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataState", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataState", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataState : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataOutput", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataOutput", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataOutput : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isCollection = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isCollection")]
        public bool IsCollection
        {
            get
            {
                return _isCollection;
            }
            set
            {
                if (!_isCollection.Equals(value))
                {
                _isCollection = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tInputSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("inputSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class InputSet : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<string> _dataInputRefs;
        
        [XmlElement("dataInputRefs", Order=0)]
        public List<string> DataInputRefs
        {
            get
            {
                return _dataInputRefs;
            }
            set
            {
                if (_dataInputRefs == value)
                    return;
                if (_dataInputRefs == null || value == null || !_dataInputRefs.SequenceEqual(value))
                {
                _dataInputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataInputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataInputRefsSpecified
        {
            get
            {
                return (this.DataInputRefs.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="InputSet" /> class.</para>
        /// </summary>
        public InputSet()
        {
            this._dataInputRefs = new List<string>();
            this._optionalInputRefs = new List<string>();
            this._whileExecutingInputRefs = new List<string>();
            this._outputSetRefs = new List<string>();
        }
        
        [XmlIgnore]
        private List<string> _optionalInputRefs;
        
        [XmlElement("optionalInputRefs", Order=1)]
        public List<string> OptionalInputRefs
        {
            get
            {
                return _optionalInputRefs;
            }
            set
            {
                if (_optionalInputRefs == value)
                    return;
                if (_optionalInputRefs == null || value == null || !_optionalInputRefs.SequenceEqual(value))
                {
                _optionalInputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the OptionalInputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OptionalInputRefsSpecified
        {
            get
            {
                return (this.OptionalInputRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<string> _whileExecutingInputRefs;
        
        [XmlElement("whileExecutingInputRefs", Order=2)]
        public List<string> WhileExecutingInputRefs
        {
            get
            {
                return _whileExecutingInputRefs;
            }
            set
            {
                if (_whileExecutingInputRefs == value)
                    return;
                if (_whileExecutingInputRefs == null || value == null || !_whileExecutingInputRefs.SequenceEqual(value))
                {
                _whileExecutingInputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the WhileExecutingInputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool WhileExecutingInputRefsSpecified
        {
            get
            {
                return (this.WhileExecutingInputRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<string> _outputSetRefs;
        
        [XmlElement("outputSetRefs", Order=3)]
        public List<string> OutputSetRefs
        {
            get
            {
                return _outputSetRefs;
            }
            set
            {
                if (_outputSetRefs == value)
                    return;
                if (_outputSetRefs == null || value == null || !_outputSetRefs.SequenceEqual(value))
                {
                _outputSetRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the OutputSetRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutputSetRefsSpecified
        {
            get
            {
                return (this.OutputSetRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tOutputSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("outputSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class OutputSet : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<string> _dataOutputRefs;
        
        [XmlElement("dataOutputRefs", Order=0)]
        public List<string> DataOutputRefs
        {
            get
            {
                return _dataOutputRefs;
            }
            set
            {
                if (_dataOutputRefs == value)
                    return;
                if (_dataOutputRefs == null || value == null || !_dataOutputRefs.SequenceEqual(value))
                {
                _dataOutputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataOutputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataOutputRefsSpecified
        {
            get
            {
                return (this.DataOutputRefs.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="OutputSet" /> class.</para>
        /// </summary>
        public OutputSet()
        {
            this._dataOutputRefs = new List<string>();
            this._optionalOutputRefs = new List<string>();
            this._whileExecutingOutputRefs = new List<string>();
            this._inputSetRefs = new List<string>();
        }
        
        [XmlIgnore]
        private List<string> _optionalOutputRefs;
        
        [XmlElement("optionalOutputRefs", Order=1)]
        public List<string> OptionalOutputRefs
        {
            get
            {
                return _optionalOutputRefs;
            }
            set
            {
                if (_optionalOutputRefs == value)
                    return;
                if (_optionalOutputRefs == null || value == null || !_optionalOutputRefs.SequenceEqual(value))
                {
                _optionalOutputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the OptionalOutputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OptionalOutputRefsSpecified
        {
            get
            {
                return (this.OptionalOutputRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<string> _whileExecutingOutputRefs;
        
        [XmlElement("whileExecutingOutputRefs", Order=2)]
        public List<string> WhileExecutingOutputRefs
        {
            get
            {
                return _whileExecutingOutputRefs;
            }
            set
            {
                if (_whileExecutingOutputRefs == value)
                    return;
                if (_whileExecutingOutputRefs == null || value == null || !_whileExecutingOutputRefs.SequenceEqual(value))
                {
                _whileExecutingOutputRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the WhileExecutingOutputRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool WhileExecutingOutputRefsSpecified
        {
            get
            {
                return (this.WhileExecutingOutputRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<string> _inputSetRefs;
        
        [XmlElement("inputSetRefs", Order=3)]
        public List<string> InputSetRefs
        {
            get
            {
                return _inputSetRefs;
            }
            set
            {
                if (_inputSetRefs == value)
                    return;
                if (_inputSetRefs == null || value == null || !_inputSetRefs.SequenceEqual(value))
                {
                _inputSetRefs = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InputSetRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InputSetRefsSpecified
        {
            get
            {
                return (this.InputSetRefs.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tProperty", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("property", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Property : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataInputAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataInputAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataInputAssociation : DataAssociation, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tDataAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(DataInputAssociation))]
    [XmlInclude(typeof(DataOutputAssociation))]
    public partial class DataAssociation : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<string> _sourceRef;
        
        [XmlElement("sourceRef", Order=0)]
        public List<string> SourceRef
        {
            get
            {
                return _sourceRef;
            }
            set
            {
                if (_sourceRef == value)
                    return;
                if (_sourceRef == null || value == null || !_sourceRef.SequenceEqual(value))
                {
                _sourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the SourceRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool SourceRefSpecified
        {
            get
            {
                return (this.SourceRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="DataAssociation" /> class.</para>
        /// </summary>
        public DataAssociation()
        {
            this._sourceRef = new List<string>();
            this._assignment = new List<Assignment>();
        }
        
        [XmlIgnore]
        private string _targetRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("targetRef", Order=1)]
        public string TargetRef
        {
            get
            {
                return _targetRef;
            }
            set
            {
                if (_targetRef == value)
                    return;
                if (_targetRef == null || value == null || !_targetRef.Equals(value))
                {
                _targetRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private FormalExpression _transformation;
        
        [XmlElement("transformation", Order=2)]
        public FormalExpression Transformation
        {
            get
            {
                return _transformation;
            }
            set
            {
                if (_transformation == value)
                    return;
                if (_transformation == null || value == null || !_transformation.Equals(value))
                {
                _transformation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<Assignment> _assignment;
        
        [XmlElement("assignment", Order=3)]
        public List<Assignment> Assignment
        {
            get
            {
                return _assignment;
            }
            set
            {
                if (_assignment == value)
                    return;
                if (_assignment == null || value == null || !_assignment.SequenceEqual(value))
                {
                _assignment = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Assignment collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AssignmentSpecified
        {
            get
            {
                return (this.Assignment.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tFormalExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("formalExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class FormalExpression : Expression, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _language;
        
        [XmlAttribute("language")]
        public string Language
        {
            get
            {
                return _language;
            }
            set
            {
                if (_language == value)
                    return;
                if (_language == null || value == null || !_language.Equals(value))
                {
                _language = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _evaluatesToTypeRef;
        
        [XmlAttribute("evaluatesToTypeRef")]
        public XmlQualifiedName EvaluatesToTypeRef
        {
            get
            {
                return _evaluatesToTypeRef;
            }
            set
            {
                if (_evaluatesToTypeRef == value)
                    return;
                if (_evaluatesToTypeRef == null || value == null || !_evaluatesToTypeRef.Equals(value))
                {
                _evaluatesToTypeRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("expression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(FormalExpression))]
    public partial class Expression : BaseElementWithMixedContent, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tBaseElementWithMixedContent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("baseElementWithMixedContent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(Expression))]
    [XmlInclude(typeof(FormalExpression))]
    public abstract partial class BaseElementWithMixedContent : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<Documentation> _documentation;
        
        [XmlElement("documentation", Order=0)]
        public List<Documentation> Documentation
        {
            get
            {
                return _documentation;
            }
            set
            {
                if (_documentation == value)
                    return;
                if (_documentation == null || value == null || !_documentation.SequenceEqual(value))
                {
                _documentation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Documentation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DocumentationSpecified
        {
            get
            {
                return (this.Documentation.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="BaseElementWithMixedContent" /> class.</para>
        /// </summary>
        public BaseElementWithMixedContent()
        {
            this._documentation = new List<Documentation>();
            this._anyAttribute = new List<XmlAttribute>();
        }
        
        [XmlIgnore]
        private ExtensionElements _extensionElements;
        
        [XmlElement("extensionElements", Order=1)]
        public ExtensionElements ExtensionElements
        {
            get
            {
                return _extensionElements;
            }
            set
            {
                if (_extensionElements == value)
                    return;
                if (_extensionElements == null || value == null || !_extensionElements.Equals(value))
                {
                _extensionElements = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlAttribute> _anyAttribute;

        [XmlAnyAttributeAttribute]
        public List<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            set
            {
                if (_anyAttribute == value)
                    return;
                if (_anyAttribute == null || value == null || !_anyAttribute.SequenceEqual(value))
                {
                _anyAttribute = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AnyAttributeSpecified
        {
            get
            {
                return (this.AnyAttribute.Count != 0);
            }
        }
        
        [XmlTextAttribute()]
        public string[] Text { get; set; }
    }
    
    
    [Serializable]
    [XmlType("tAssignment", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("assignment", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Assignment : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _from;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("from", Order=0)]
        public Expression From
        {
            get
            {
                return _from;
            }
            set
            {
                if (_from == value)
                    return;
                if (_from == null || value == null || !_from.Equals(value))
                {
                _from = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private Expression _to;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("to", Order=1)]
        public Expression To
        {
            get
            {
                return _to;
            }
            set
            {
                if (_to == value)
                    return;
                if (_to == null || value == null || !_to.Equals(value))
                {
                _to = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataOutputAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataOutputAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataOutputAssociation : DataAssociation, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tResourceRole", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("resourceRole", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(HumanPerformer))]
    [XmlInclude(typeof(Performer))]
    [XmlInclude(typeof(PotentialOwner))]
    public partial class ResourceRole : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _resourceRef;
        
        [XmlElement("resourceRef", Order=0)]
        public XmlQualifiedName ResourceRef
        {
            get
            {
                return _resourceRef;
            }
            set
            {
                if (_resourceRef == value)
                    return;
                if (_resourceRef == null || value == null || !_resourceRef.Equals(value))
                {
                _resourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<ResourceParameterBinding> _resourceParameterBinding;
        
        [XmlElement("resourceParameterBinding", Order=1)]
        public List<ResourceParameterBinding> ResourceParameterBinding
        {
            get
            {
                return _resourceParameterBinding;
            }
            set
            {
                if (_resourceParameterBinding == value)
                    return;
                if (_resourceParameterBinding == null || value == null || !_resourceParameterBinding.SequenceEqual(value))
                {
                _resourceParameterBinding = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ResourceParameterBinding collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ResourceParameterBindingSpecified
        {
            get
            {
                return (this.ResourceParameterBinding.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ResourceRole" /> class.</para>
        /// </summary>
        public ResourceRole()
        {
            this._resourceParameterBinding = new List<ResourceParameterBinding>();
        }
        
        [XmlIgnore]
        private ResourceAssignmentExpression _resourceAssignmentExpression;
        
        [XmlElement("resourceAssignmentExpression", Order=2)]
        public ResourceAssignmentExpression ResourceAssignmentExpression
        {
            get
            {
                return _resourceAssignmentExpression;
            }
            set
            {
                if (_resourceAssignmentExpression == value)
                    return;
                if (_resourceAssignmentExpression == null || value == null || !_resourceAssignmentExpression.Equals(value))
                {
                _resourceAssignmentExpression = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tResourceParameterBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("resourceParameterBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ResourceParameterBinding : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _expression;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("formalExpression", Type=typeof(FormalExpression), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("expression", Order=0)]
        public Expression Expression
        {
            get
            {
                return _expression;
            }
            set
            {
                if (_expression == value)
                    return;
                if (_expression == null || value == null || !_expression.Equals(value))
                {
                _expression = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _parameterRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("parameterRef")]
        public XmlQualifiedName ParameterRef
        {
            get
            {
                return _parameterRef;
            }
            set
            {
                if (_parameterRef == value)
                    return;
                if (_parameterRef == null || value == null || !_parameterRef.Equals(value))
                {
                _parameterRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tResourceAssignmentExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("resourceAssignmentExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ResourceAssignmentExpression : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _expression;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("formalExpression", Type=typeof(FormalExpression), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("expression", Order=0)]
        public Expression Expression
        {
            get
            {
                return _expression;
            }
            set
            {
                if (_expression == value)
                    return;
                if (_expression == null || value == null || !_expression.Equals(value))
                {
                _expression = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("loopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(MultiInstanceLoopCharacteristics))]
    [XmlInclude(typeof(StandardLoopCharacteristics))]
    public abstract partial class LoopCharacteristics : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tAdHocSubProcess", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("adHocSubProcess", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class AdHocSubProcess : SubProcess, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _completionCondition;
        
        [XmlElement("completionCondition", Order=0)]
        public Expression CompletionCondition
        {
            get
            {
                return _completionCondition;
            }
            set
            {
                if (_completionCondition == value)
                    return;
                if (_completionCondition == null || value == null || !_completionCondition.Equals(value))
                {
                _completionCondition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _cancelRemainingInstances = true;
        
        [DefaultValueAttribute(true)]
        [XmlAttribute("cancelRemainingInstances")]
        public bool CancelRemainingInstances
        {
            get
            {
                return _cancelRemainingInstances;
            }
            set
            {
                if (!_cancelRemainingInstances.Equals(value))
                {
                _cancelRemainingInstances = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private AdHocOrdering _ordering;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ordering")]
        public AdHocOrdering OrderingValue
        {
            get
            {
                return _ordering;
            }
            set
            {
                if (!_ordering.Equals(value))
                {
                _ordering = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Ordering property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool OrderingValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<AdHocOrdering> Ordering
        {
            get
            {
                if (this.OrderingValueSpecified)
                {
                    return this.OrderingValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.OrderingValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.OrderingValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.OrderingValue = value.GetValueOrDefault();
                    this.OrderingValueSpecified = value.HasValue;
                    OnPropertyChanged("Ordering");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSubProcess", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("subProcess", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(AdHocSubProcess))]
    [XmlInclude(typeof(Transaction))]
    public partial class SubProcess : Activity, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<LaneSet> _laneSet;
        
        [XmlElement("laneSet", Order=0)]
        public List<LaneSet> LaneSet
        {
            get
            {
                return _laneSet;
            }
            set
            {
                if (_laneSet == value)
                    return;
                if (_laneSet == null || value == null || !_laneSet.SequenceEqual(value))
                {
                _laneSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the LaneSet collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool LaneSetSpecified
        {
            get
            {
                return (this.LaneSet.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="SubProcess" /> class.</para>
        /// </summary>
        public SubProcess()
        {
            this._laneSet = new List<LaneSet>();
            this._flowElement = new List<FlowElement>();
            this._artifact = new List<Artifact>();
        }
        
        [XmlIgnore]
        private List<FlowElement> _flowElement;
        
        [XmlElement("adHocSubProcess", Type=typeof(AdHocSubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("boundaryEvent", Type=typeof(BoundaryEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("businessRuleTask", Type=typeof(BusinessRuleTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("callActivity", Type=typeof(CallActivity), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("callChoreography", Type=typeof(CallChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("choreographyTask", Type=typeof(ChoreographyTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("complexGateway", Type=typeof(ComplexGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("dataObject", Type=typeof(DataObject), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("dataObjectReference", Type=typeof(DataObjectReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("dataStoreReference", Type=typeof(DataStoreReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("endEvent", Type=typeof(EndEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("event", Type=typeof(Event), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("eventBasedGateway", Type=typeof(EventBasedGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("exclusiveGateway", Type=typeof(ExclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("implicitThrowEvent", Type=typeof(ImplicitThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("inclusiveGateway", Type=typeof(InclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("intermediateCatchEvent", Type=typeof(IntermediateCatchEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("intermediateThrowEvent", Type=typeof(IntermediateThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("manualTask", Type=typeof(ManualTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("parallelGateway", Type=typeof(ParallelGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("receiveTask", Type=typeof(ReceiveTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("scriptTask", Type=typeof(ScriptTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("sendTask", Type=typeof(SendTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("sequenceFlow", Type=typeof(SequenceFlow), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("serviceTask", Type=typeof(ServiceTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("startEvent", Type=typeof(StartEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("subChoreography", Type=typeof(SubChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("subProcess", Type=typeof(SubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("task", Type=typeof(Task), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("transaction", Type=typeof(Transaction), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("userTask", Type=typeof(UserTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("flowElement", Order=1)]
        public List<FlowElement> FlowElement
        {
            get
            {
                return _flowElement;
            }
            set
            {
                if (_flowElement == value)
                    return;
                if (_flowElement == null || value == null || !_flowElement.SequenceEqual(value))
                {
                _flowElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FlowElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool FlowElementSpecified
        {
            get
            {
                return (this.FlowElement.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<Artifact> _artifact;
        
        [XmlElement("association", Type=typeof(Association), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("group", Type=typeof(Group), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("textAnnotation", Type=typeof(TextAnnotation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("artifact", Order=2)]
        public List<Artifact> Artifact
        {
            get
            {
                return _artifact;
            }
            set
            {
                if (_artifact == value)
                    return;
                if (_artifact == null || value == null || !_artifact.SequenceEqual(value))
                {
                _artifact = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Artifact collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ArtifactSpecified
        {
            get
            {
                return (this.Artifact.Count != 0);
            }
        }
        
        [XmlIgnore]
        private bool _triggeredByEvent = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("triggeredByEvent")]
        public bool TriggeredByEvent
        {
            get
            {
                return _triggeredByEvent;
            }
            set
            {
                if (!_triggeredByEvent.Equals(value))
                {
                _triggeredByEvent = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tLaneSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("laneSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class LaneSet : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Lane> _lane;
        
        [XmlElement("lane", Order=0)]
        public List<Lane> Lane
        {
            get
            {
                return _lane;
            }
            set
            {
                if (_lane == value)
                    return;
                if (_lane == null || value == null || !_lane.SequenceEqual(value))
                {
                _lane = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Lane collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool LaneSpecified
        {
            get
            {
                return (this.Lane.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="LaneSet" /> class.</para>
        /// </summary>
        public LaneSet()
        {
            this._lane = new List<Lane>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tLane", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("lane", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Lane : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private BaseElement _partitionElement;
        
        [XmlElement("partitionElement", Order=0)]
        public BaseElement PartitionElement
        {
            get
            {
                return _partitionElement;
            }
            set
            {
                if (_partitionElement == value)
                    return;
                if (_partitionElement == null || value == null || !_partitionElement.Equals(value))
                {
                _partitionElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<string> _flowNodeRef;
        
        [XmlElement("flowNodeRef", Order=1)]
        public List<string> FlowNodeRef
        {
            get
            {
                return _flowNodeRef;
            }
            set
            {
                if (_flowNodeRef == value)
                    return;
                if (_flowNodeRef == null || value == null || !_flowNodeRef.SequenceEqual(value))
                {
                _flowNodeRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FlowNodeRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool FlowNodeRefSpecified
        {
            get
            {
                return (this.FlowNodeRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Lane" /> class.</para>
        /// </summary>
        public Lane()
        {
            this._flowNodeRef = new List<string>();
        }
        
        [XmlIgnore]
        private LaneSet _childLaneSet;
        
        [XmlElement("childLaneSet", Order=2)]
        public LaneSet ChildLaneSet
        {
            get
            {
                return _childLaneSet;
            }
            set
            {
                if (_childLaneSet == value)
                    return;
                if (_childLaneSet == null || value == null || !_childLaneSet.Equals(value))
                {
                _childLaneSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _partitionElementRef;
        
        [XmlAttribute("partitionElementRef")]
        public XmlQualifiedName PartitionElementRef
        {
            get
            {
                return _partitionElementRef;
            }
            set
            {
                if (_partitionElementRef == value)
                    return;
                if (_partitionElementRef == null || value == null || !_partitionElementRef.Equals(value))
                {
                _partitionElementRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tArtifact", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("artifact", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(Association))]
    [XmlInclude(typeof(Group))]
    [XmlInclude(typeof(TextAnnotation))]
    public abstract partial class Artifact : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tAdHocOrdering", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum AdHocOrdering
    {
        
        Parallel,
        
        Sequential,
    }
    
    
    [Serializable]
    [XmlType("tAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("association", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Association : Artifact, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _sourceRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("sourceRef")]
        public XmlQualifiedName SourceRef
        {
            get
            {
                return _sourceRef;
            }
            set
            {
                if (_sourceRef == value)
                    return;
                if (_sourceRef == null || value == null || !_sourceRef.Equals(value))
                {
                _sourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _targetRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("targetRef")]
        public XmlQualifiedName TargetRef
        {
            get
            {
                return _targetRef;
            }
            set
            {
                if (_targetRef == value)
                    return;
                if (_targetRef == null || value == null || !_targetRef.Equals(value))
                {
                _targetRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private AssociationDirection _associationDirection = AssociationDirection.None;
        
        [DefaultValueAttribute(AssociationDirection.None)]
        [XmlAttribute("associationDirection")]
        public AssociationDirection AssociationDirection
        {
            get
            {
                return _associationDirection;
            }
            set
            {
                if (!_associationDirection.Equals(value))
                {
                _associationDirection = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tAssociationDirection", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum AssociationDirection
    {
        
        None,
        
        One,
        
        Both,
    }
    
    
    [Serializable]
    [XmlType("tBoundaryEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("boundaryEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class BoundaryEvent : CatchEvent, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private bool _cancelActivity = true;
        
        [DefaultValueAttribute(true)]
        [XmlAttribute("cancelActivity")]
        public bool CancelActivity
        {
            get
            {
                return _cancelActivity;
            }
            set
            {
                if (!_cancelActivity.Equals(value))
                {
                _cancelActivity = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _attachedToRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("attachedToRef")]
        public XmlQualifiedName AttachedToRef
        {
            get
            {
                return _attachedToRef;
            }
            set
            {
                if (_attachedToRef == value)
                    return;
                if (_attachedToRef == null || value == null || !_attachedToRef.Equals(value))
                {
                _attachedToRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCatchEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("catchEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(BoundaryEvent))]
    [XmlInclude(typeof(IntermediateCatchEvent))]
    [XmlInclude(typeof(StartEvent))]
    public abstract partial class CatchEvent : Event, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<DataOutput> _dataOutput;
        
        [XmlElement("dataOutput", Order=0)]
        public List<DataOutput> DataOutput
        {
            get
            {
                return _dataOutput;
            }
            set
            {
                if (_dataOutput == value)
                    return;
                if (_dataOutput == null || value == null || !_dataOutput.SequenceEqual(value))
                {
                _dataOutput = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataOutput collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataOutputSpecified
        {
            get
            {
                return (this.DataOutput.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CatchEvent" /> class.</para>
        /// </summary>
        public CatchEvent()
        {
            this._dataOutput = new List<DataOutput>();
            this._dataOutputAssociation = new List<DataOutputAssociation>();
            this._eventDefinition = new List<EventDefinition>();
            this._eventDefinitionRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<DataOutputAssociation> _dataOutputAssociation;
        
        [XmlElement("dataOutputAssociation", Order=1)]
        public List<DataOutputAssociation> DataOutputAssociation
        {
            get
            {
                return _dataOutputAssociation;
            }
            set
            {
                if (_dataOutputAssociation == value)
                    return;
                if (_dataOutputAssociation == null || value == null || !_dataOutputAssociation.SequenceEqual(value))
                {
                _dataOutputAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataOutputAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataOutputAssociationSpecified
        {
            get
            {
                return (this.DataOutputAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private OutputSet _outputSet;
        
        [XmlElement("outputSet", Order=2)]
        public OutputSet OutputSet
        {
            get
            {
                return _outputSet;
            }
            set
            {
                if (_outputSet == value)
                    return;
                if (_outputSet == null || value == null || !_outputSet.Equals(value))
                {
                _outputSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<EventDefinition> _eventDefinition;
        
        [XmlElement("cancelEventDefinition", Type=typeof(CancelEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("compensateEventDefinition", Type=typeof(CompensateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("conditionalEventDefinition", Type=typeof(ConditionalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("errorEventDefinition", Type=typeof(ErrorEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("escalationEventDefinition", Type=typeof(EscalationEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("linkEventDefinition", Type=typeof(LinkEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("messageEventDefinition", Type=typeof(MessageEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("signalEventDefinition", Type=typeof(SignalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("terminateEventDefinition", Type=typeof(TerminateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("timerEventDefinition", Type=typeof(TimerEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("eventDefinition", Order=3)]
        public List<EventDefinition> EventDefinition
        {
            get
            {
                return _eventDefinition;
            }
            set
            {
                if (_eventDefinition == value)
                    return;
                if (_eventDefinition == null || value == null || !_eventDefinition.SequenceEqual(value))
                {
                _eventDefinition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EventDefinition collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EventDefinitionSpecified
        {
            get
            {
                return (this.EventDefinition.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _eventDefinitionRef;
        
        [XmlElement("eventDefinitionRef", Order=4)]
        public List<XmlQualifiedName> EventDefinitionRef
        {
            get
            {
                return _eventDefinitionRef;
            }
            set
            {
                if (_eventDefinitionRef == value)
                    return;
                if (_eventDefinitionRef == null || value == null || !_eventDefinitionRef.SequenceEqual(value))
                {
                _eventDefinitionRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EventDefinitionRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EventDefinitionRefSpecified
        {
            get
            {
                return (this.EventDefinitionRef.Count != 0);
            }
        }
        
        [XmlIgnore]
        private bool _parallelMultiple = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("parallelMultiple")]
        public bool ParallelMultiple
        {
            get
            {
                return _parallelMultiple;
            }
            set
            {
                if (!_parallelMultiple.Equals(value))
                {
                _parallelMultiple = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("event", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(BoundaryEvent))]
    [XmlInclude(typeof(CatchEvent))]
    [XmlInclude(typeof(EndEvent))]
    [XmlInclude(typeof(ImplicitThrowEvent))]
    [XmlInclude(typeof(IntermediateCatchEvent))]
    [XmlInclude(typeof(IntermediateThrowEvent))]
    [XmlInclude(typeof(StartEvent))]
    [XmlInclude(typeof(ThrowEvent))]
    public abstract partial class Event : FlowNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Property> _property;
        
        [XmlElement("property", Order=0)]
        public List<Property> Property
        {
            get
            {
                return _property;
            }
            set
            {
                if (_property == value)
                    return;
                if (_property == null || value == null || !_property.SequenceEqual(value))
                {
                _property = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Property collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool PropertySpecified
        {
            get
            {
                return (this.Property.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Event" /> class.</para>
        /// </summary>
        public Event()
        {
            this._property = new List<Property>();
        }
    }
    
    
    [Serializable]
    [XmlType("tEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("eventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class EventDefinition : RootElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tRootElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("rootElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
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
    public abstract partial class RootElement : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tBusinessRuleTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("businessRuleTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class BusinessRuleTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _implementation = "##unspecified";
        
        [DefaultValueAttribute("##unspecified")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("task", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(BusinessRuleTask))]
    [XmlInclude(typeof(ManualTask))]
    [XmlInclude(typeof(ReceiveTask))]
    [XmlInclude(typeof(ScriptTask))]
    [XmlInclude(typeof(SendTask))]
    [XmlInclude(typeof(ServiceTask))]
    [XmlInclude(typeof(UserTask))]
    public partial class Task : Activity, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tCallableElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("callableElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(GlobalBusinessRuleTask))]
    [XmlInclude(typeof(GlobalManualTask))]
    [XmlInclude(typeof(GlobalScriptTask))]
    [XmlInclude(typeof(GlobalTask))]
    [XmlInclude(typeof(GlobalUserTask))]
    [XmlInclude(typeof(Process))]
    public partial class CallableElement : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _supportedInterfaceRef;
        
        [XmlElement("supportedInterfaceRef", Order=0)]
        public List<XmlQualifiedName> SupportedInterfaceRef
        {
            get
            {
                return _supportedInterfaceRef;
            }
            set
            {
                if (_supportedInterfaceRef == value)
                    return;
                if (_supportedInterfaceRef == null || value == null || !_supportedInterfaceRef.SequenceEqual(value))
                {
                _supportedInterfaceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the SupportedInterfaceRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool SupportedInterfaceRefSpecified
        {
            get
            {
                return (this.SupportedInterfaceRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CallableElement" /> class.</para>
        /// </summary>
        public CallableElement()
        {
            this._supportedInterfaceRef = new List<XmlQualifiedName>();
            this._ioBinding = new List<InputOutputBinding>();
        }
        
        [XmlIgnore]
        private InputOutputSpecification _ioSpecification;
        
        [XmlElement("ioSpecification", Order=1)]
        public InputOutputSpecification IoSpecification
        {
            get
            {
                return _ioSpecification;
            }
            set
            {
                if (_ioSpecification == value)
                    return;
                if (_ioSpecification == null || value == null || !_ioSpecification.Equals(value))
                {
                _ioSpecification = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<InputOutputBinding> _ioBinding;
        
        [XmlElement("ioBinding", Order=2)]
        public List<InputOutputBinding> IoBinding
        {
            get
            {
                return _ioBinding;
            }
            set
            {
                if (_ioBinding == value)
                    return;
                if (_ioBinding == null || value == null || !_ioBinding.SequenceEqual(value))
                {
                _ioBinding = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the IoBinding collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool IoBindingSpecified
        {
            get
            {
                return (this.IoBinding.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tInputOutputBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("ioBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class InputOutputBinding : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _operationRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("operationRef")]
        public XmlQualifiedName OperationRef
        {
            get
            {
                return _operationRef;
            }
            set
            {
                if (_operationRef == value)
                    return;
                if (_operationRef == null || value == null || !_operationRef.Equals(value))
                {
                _operationRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _inputDataRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("inputDataRef")]
        public string InputDataRef
        {
            get
            {
                return _inputDataRef;
            }
            set
            {
                if (_inputDataRef == value)
                    return;
                if (_inputDataRef == null || value == null || !_inputDataRef.Equals(value))
                {
                _inputDataRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _outputDataRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("outputDataRef")]
        public string OutputDataRef
        {
            get
            {
                return _outputDataRef;
            }
            set
            {
                if (_outputDataRef == value)
                    return;
                if (_outputDataRef == null || value == null || !_outputDataRef.Equals(value))
                {
                _outputDataRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCallActivity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("callActivity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CallActivity : Activity, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _calledElement;
        
        [XmlAttribute("calledElement")]
        public XmlQualifiedName CalledElement
        {
            get
            {
                return _calledElement;
            }
            set
            {
                if (_calledElement == value)
                    return;
                if (_calledElement == null || value == null || !_calledElement.Equals(value))
                {
                _calledElement = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCallChoreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("callChoreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CallChoreography : ChoreographyActivity, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<ParticipantAssociation> _participantAssociation;
        
        [XmlElement("participantAssociation", Order=0)]
        public List<ParticipantAssociation> ParticipantAssociation
        {
            get
            {
                return _participantAssociation;
            }
            set
            {
                if (_participantAssociation == value)
                    return;
                if (_participantAssociation == null || value == null || !_participantAssociation.SequenceEqual(value))
                {
                _participantAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantAssociationSpecified
        {
            get
            {
                return (this.ParticipantAssociation.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CallChoreography" /> class.</para>
        /// </summary>
        public CallChoreography()
        {
            this._participantAssociation = new List<ParticipantAssociation>();
        }
        
        [XmlIgnore]
        private XmlQualifiedName _calledChoreographyRef;
        
        [XmlAttribute("calledChoreographyRef")]
        public XmlQualifiedName CalledChoreographyRef
        {
            get
            {
                return _calledChoreographyRef;
            }
            set
            {
                if (_calledChoreographyRef == value)
                    return;
                if (_calledChoreographyRef == null || value == null || !_calledChoreographyRef.Equals(value))
                {
                _calledChoreographyRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tChoreographyActivity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("choreographyActivity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(CallChoreography))]
    [XmlInclude(typeof(ChoreographyTask))]
    [XmlInclude(typeof(SubChoreography))]
    public abstract partial class ChoreographyActivity : FlowNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _participantRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("participantRef", Order=0)]
        public List<XmlQualifiedName> ParticipantRef
        {
            get
            {
                return _participantRef;
            }
            set
            {
                if (_participantRef == value)
                    return;
                if (_participantRef == null || value == null || !_participantRef.SequenceEqual(value))
                {
                _participantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ChoreographyActivity" /> class.</para>
        /// </summary>
        public ChoreographyActivity()
        {
            this._participantRef = new List<XmlQualifiedName>();
            this._correlationKey = new List<CorrelationKey>();
        }
        
        [XmlIgnore]
        private List<CorrelationKey> _correlationKey;
        
        [XmlElement("correlationKey", Order=1)]
        public List<CorrelationKey> CorrelationKey
        {
            get
            {
                return _correlationKey;
            }
            set
            {
                if (_correlationKey == value)
                    return;
                if (_correlationKey == null || value == null || !_correlationKey.SequenceEqual(value))
                {
                _correlationKey = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationKey collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationKeySpecified
        {
            get
            {
                return (this.CorrelationKey.Count != 0);
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _initiatingParticipantRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("initiatingParticipantRef")]
        public XmlQualifiedName InitiatingParticipantRef
        {
            get
            {
                return _initiatingParticipantRef;
            }
            set
            {
                if (_initiatingParticipantRef == value)
                    return;
                if (_initiatingParticipantRef == null || value == null || !_initiatingParticipantRef.Equals(value))
                {
                _initiatingParticipantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private ChoreographyLoopType _loopType = ChoreographyLoopType.None;
        
        [DefaultValueAttribute(ChoreographyLoopType.None)]
        [XmlAttribute("loopType")]
        public ChoreographyLoopType LoopType
        {
            get
            {
                return _loopType;
            }
            set
            {
                if (!_loopType.Equals(value))
                {
                _loopType = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCorrelationKey", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("correlationKey", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CorrelationKey : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _correlationPropertyRef;
        
        [XmlElement("correlationPropertyRef", Order=0)]
        public List<XmlQualifiedName> CorrelationPropertyRef
        {
            get
            {
                return _correlationPropertyRef;
            }
            set
            {
                if (_correlationPropertyRef == value)
                    return;
                if (_correlationPropertyRef == null || value == null || !_correlationPropertyRef.SequenceEqual(value))
                {
                _correlationPropertyRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationPropertyRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationPropertyRefSpecified
        {
            get
            {
                return (this.CorrelationPropertyRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CorrelationKey" /> class.</para>
        /// </summary>
        public CorrelationKey()
        {
            this._correlationPropertyRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tChoreographyLoopType", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum ChoreographyLoopType
    {
        
        None,
        
        Standard,
        
        MultiInstanceSequential,
        
        MultiInstanceParallel,
    }
    
    
    [Serializable]
    [XmlType("tParticipantAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("participantAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ParticipantAssociation : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _innerParticipantRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("innerParticipantRef", Order=0)]
        public XmlQualifiedName InnerParticipantRef
        {
            get
            {
                return _innerParticipantRef;
            }
            set
            {
                if (_innerParticipantRef == value)
                    return;
                if (_innerParticipantRef == null || value == null || !_innerParticipantRef.Equals(value))
                {
                _innerParticipantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _outerParticipantRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("outerParticipantRef", Order=1)]
        public XmlQualifiedName OuterParticipantRef
        {
            get
            {
                return _outerParticipantRef;
            }
            set
            {
                if (_outerParticipantRef == value)
                    return;
                if (_outerParticipantRef == null || value == null || !_outerParticipantRef.Equals(value))
                {
                _outerParticipantRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCallConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("callConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CallConversation : ConversationNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<ParticipantAssociation> _participantAssociation;
        
        [XmlElement("participantAssociation", Order=0)]
        public List<ParticipantAssociation> ParticipantAssociation
        {
            get
            {
                return _participantAssociation;
            }
            set
            {
                if (_participantAssociation == value)
                    return;
                if (_participantAssociation == null || value == null || !_participantAssociation.SequenceEqual(value))
                {
                _participantAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantAssociationSpecified
        {
            get
            {
                return (this.ParticipantAssociation.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CallConversation" /> class.</para>
        /// </summary>
        public CallConversation()
        {
            this._participantAssociation = new List<ParticipantAssociation>();
        }
        
        [XmlIgnore]
        private XmlQualifiedName _calledCollaborationRef;
        
        [XmlAttribute("calledCollaborationRef")]
        public XmlQualifiedName CalledCollaborationRef
        {
            get
            {
                return _calledCollaborationRef;
            }
            set
            {
                if (_calledCollaborationRef == value)
                    return;
                if (_calledCollaborationRef == null || value == null || !_calledCollaborationRef.Equals(value))
                {
                _calledCollaborationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tConversationNode", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conversationNode", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(CallConversation))]
    [XmlInclude(typeof(Conversation))]
    [XmlInclude(typeof(SubConversation))]
    public abstract partial class ConversationNode : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _participantRef;
        
        [XmlElement("participantRef", Order=0)]
        public List<XmlQualifiedName> ParticipantRef
        {
            get
            {
                return _participantRef;
            }
            set
            {
                if (_participantRef == value)
                    return;
                if (_participantRef == null || value == null || !_participantRef.SequenceEqual(value))
                {
                _participantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantRefSpecified
        {
            get
            {
                return (this.ParticipantRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ConversationNode" /> class.</para>
        /// </summary>
        public ConversationNode()
        {
            this._participantRef = new List<XmlQualifiedName>();
            this._messageFlowRef = new List<XmlQualifiedName>();
            this._correlationKey = new List<CorrelationKey>();
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _messageFlowRef;
        
        [XmlElement("messageFlowRef", Order=1)]
        public List<XmlQualifiedName> MessageFlowRef
        {
            get
            {
                return _messageFlowRef;
            }
            set
            {
                if (_messageFlowRef == value)
                    return;
                if (_messageFlowRef == null || value == null || !_messageFlowRef.SequenceEqual(value))
                {
                _messageFlowRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the MessageFlowRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool MessageFlowRefSpecified
        {
            get
            {
                return (this.MessageFlowRef.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<CorrelationKey> _correlationKey;
        
        [XmlElement("correlationKey", Order=2)]
        public List<CorrelationKey> CorrelationKey
        {
            get
            {
                return _correlationKey;
            }
            set
            {
                if (_correlationKey == value)
                    return;
                if (_correlationKey == null || value == null || !_correlationKey.SequenceEqual(value))
                {
                _correlationKey = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationKey collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationKeySpecified
        {
            get
            {
                return (this.CorrelationKey.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCancelEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("cancelEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CancelEventDefinition : EventDefinition, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tCategory", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("category", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Category : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<CategoryValue> _categoryValue;
        
        [XmlElement("categoryValue", Order=0)]
        public List<CategoryValue> CategoryValue
        {
            get
            {
                return _categoryValue;
            }
            set
            {
                if (_categoryValue == value)
                    return;
                if (_categoryValue == null || value == null || !_categoryValue.SequenceEqual(value))
                {
                _categoryValue = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CategoryValue collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CategoryValueSpecified
        {
            get
            {
                return (this.CategoryValue.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Category" /> class.</para>
        /// </summary>
        public Category()
        {
            this._categoryValue = new List<CategoryValue>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCategoryValue", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("categoryValue", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CategoryValue : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _value;
        
        [XmlAttribute("value")]
        public string Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (_value == value)
                    return;
                if (_value == null || value == null || !_value.Equals(value))
                {
                _value = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tChoreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("choreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(GlobalChoreographyTask))]
    public partial class Choreography : Collaboration, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<FlowElement> _flowElement;
        
        [XmlElement("adHocSubProcess", Type=typeof(AdHocSubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("boundaryEvent", Type=typeof(BoundaryEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("businessRuleTask", Type=typeof(BusinessRuleTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("callActivity", Type=typeof(CallActivity), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("callChoreography", Type=typeof(CallChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("choreographyTask", Type=typeof(ChoreographyTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("complexGateway", Type=typeof(ComplexGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataObject", Type=typeof(DataObject), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataObjectReference", Type=typeof(DataObjectReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataStoreReference", Type=typeof(DataStoreReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("endEvent", Type=typeof(EndEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("event", Type=typeof(Event), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("eventBasedGateway", Type=typeof(EventBasedGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("exclusiveGateway", Type=typeof(ExclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("implicitThrowEvent", Type=typeof(ImplicitThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("inclusiveGateway", Type=typeof(InclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("intermediateCatchEvent", Type=typeof(IntermediateCatchEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("intermediateThrowEvent", Type=typeof(IntermediateThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("manualTask", Type=typeof(ManualTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("parallelGateway", Type=typeof(ParallelGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("receiveTask", Type=typeof(ReceiveTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("scriptTask", Type=typeof(ScriptTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("sendTask", Type=typeof(SendTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("sequenceFlow", Type=typeof(SequenceFlow), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("serviceTask", Type=typeof(ServiceTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("startEvent", Type=typeof(StartEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("subChoreography", Type=typeof(SubChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("subProcess", Type=typeof(SubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("task", Type=typeof(Task), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("transaction", Type=typeof(Transaction), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("userTask", Type=typeof(UserTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("flowElement", Order=0)]
        public List<FlowElement> FlowElement
        {
            get
            {
                return _flowElement;
            }
            set
            {
                if (_flowElement == value)
                    return;
                if (_flowElement == null || value == null || !_flowElement.SequenceEqual(value))
                {
                _flowElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FlowElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool FlowElementSpecified
        {
            get
            {
                return (this.FlowElement.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Choreography" /> class.</para>
        /// </summary>
        public Choreography()
        {
            this._flowElement = new List<FlowElement>();
        }
    }
    
    
    [Serializable]
    [XmlType("tCollaboration", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("collaboration", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(Choreography))]
    [XmlInclude(typeof(GlobalChoreographyTask))]
    [XmlInclude(typeof(GlobalConversation))]
    public partial class Collaboration : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Participant> _participant;
        
        [XmlElement("participant", Order=0)]
        public List<Participant> Participant
        {
            get
            {
                return _participant;
            }
            set
            {
                if (_participant == value)
                    return;
                if (_participant == null || value == null || !_participant.SequenceEqual(value))
                {
                _participant = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Participant collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantSpecified
        {
            get
            {
                return (this.Participant.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Collaboration" /> class.</para>
        /// </summary>
        public Collaboration()
        {
            this._participant = new List<Participant>();
            this._messageFlow = new List<MessageFlow>();
            this._artifact = new List<Artifact>();
            this._conversationNode = new List<ConversationNode>();
            this._conversationAssociation = new List<ConversationAssociation>();
            this._participantAssociation = new List<ParticipantAssociation>();
            this._messageFlowAssociation = new List<MessageFlowAssociation>();
            this._correlationKey = new List<CorrelationKey>();
            this._choreographyRef = new List<XmlQualifiedName>();
            this._conversationLink = new List<ConversationLink>();
        }
        
        [XmlIgnore]
        private List<MessageFlow> _messageFlow;
        
        [XmlElement("messageFlow", Order=1)]
        public List<MessageFlow> MessageFlow
        {
            get
            {
                return _messageFlow;
            }
            set
            {
                if (_messageFlow == value)
                    return;
                if (_messageFlow == null || value == null || !_messageFlow.SequenceEqual(value))
                {
                _messageFlow = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the MessageFlow collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool MessageFlowSpecified
        {
            get
            {
                return (this.MessageFlow.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<Artifact> _artifact;
        
        [XmlElement("association", Type=typeof(Association), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("group", Type=typeof(Group), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("textAnnotation", Type=typeof(TextAnnotation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("artifact", Order=2)]
        public List<Artifact> Artifact
        {
            get
            {
                return _artifact;
            }
            set
            {
                if (_artifact == value)
                    return;
                if (_artifact == null || value == null || !_artifact.SequenceEqual(value))
                {
                _artifact = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Artifact collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ArtifactSpecified
        {
            get
            {
                return (this.Artifact.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ConversationNode> _conversationNode;
        
        [XmlElement("callConversation", Type=typeof(CallConversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("conversation", Type=typeof(Conversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("subConversation", Type=typeof(SubConversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("conversationNode", Order=3)]
        public List<ConversationNode> ConversationNode
        {
            get
            {
                return _conversationNode;
            }
            set
            {
                if (_conversationNode == value)
                    return;
                if (_conversationNode == null || value == null || !_conversationNode.SequenceEqual(value))
                {
                _conversationNode = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ConversationNode collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ConversationNodeSpecified
        {
            get
            {
                return (this.ConversationNode.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ConversationAssociation> _conversationAssociation;
        
        [XmlElement("conversationAssociation", Order=4)]
        public List<ConversationAssociation> ConversationAssociation
        {
            get
            {
                return _conversationAssociation;
            }
            set
            {
                if (_conversationAssociation == value)
                    return;
                if (_conversationAssociation == null || value == null || !_conversationAssociation.SequenceEqual(value))
                {
                _conversationAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ConversationAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ConversationAssociationSpecified
        {
            get
            {
                return (this.ConversationAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ParticipantAssociation> _participantAssociation;
        
        [XmlElement("participantAssociation", Order=5)]
        public List<ParticipantAssociation> ParticipantAssociation
        {
            get
            {
                return _participantAssociation;
            }
            set
            {
                if (_participantAssociation == value)
                    return;
                if (_participantAssociation == null || value == null || !_participantAssociation.SequenceEqual(value))
                {
                _participantAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantAssociationSpecified
        {
            get
            {
                return (this.ParticipantAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<MessageFlowAssociation> _messageFlowAssociation;
        
        [XmlElement("messageFlowAssociation", Order=6)]
        public List<MessageFlowAssociation> MessageFlowAssociation
        {
            get
            {
                return _messageFlowAssociation;
            }
            set
            {
                if (_messageFlowAssociation == value)
                    return;
                if (_messageFlowAssociation == null || value == null || !_messageFlowAssociation.SequenceEqual(value))
                {
                _messageFlowAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the MessageFlowAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool MessageFlowAssociationSpecified
        {
            get
            {
                return (this.MessageFlowAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<CorrelationKey> _correlationKey;
        
        [XmlElement("correlationKey", Order=7)]
        public List<CorrelationKey> CorrelationKey
        {
            get
            {
                return _correlationKey;
            }
            set
            {
                if (_correlationKey == value)
                    return;
                if (_correlationKey == null || value == null || !_correlationKey.SequenceEqual(value))
                {
                _correlationKey = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationKey collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationKeySpecified
        {
            get
            {
                return (this.CorrelationKey.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _choreographyRef;
        
        [XmlElement("choreographyRef", Order=8)]
        public List<XmlQualifiedName> ChoreographyRef
        {
            get
            {
                return _choreographyRef;
            }
            set
            {
                if (_choreographyRef == value)
                    return;
                if (_choreographyRef == null || value == null || !_choreographyRef.SequenceEqual(value))
                {
                _choreographyRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ChoreographyRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ChoreographyRefSpecified
        {
            get
            {
                return (this.ChoreographyRef.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ConversationLink> _conversationLink;
        
        [XmlElement("conversationLink", Order=9)]
        public List<ConversationLink> ConversationLink
        {
            get
            {
                return _conversationLink;
            }
            set
            {
                if (_conversationLink == value)
                    return;
                if (_conversationLink == null || value == null || !_conversationLink.SequenceEqual(value))
                {
                _conversationLink = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ConversationLink collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ConversationLinkSpecified
        {
            get
            {
                return (this.ConversationLink.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isClosed = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isClosed")]
        public bool IsClosed
        {
            get
            {
                return _isClosed;
            }
            set
            {
                if (!_isClosed.Equals(value))
                {
                _isClosed = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tParticipant", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("participant", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Participant : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _interfaceRef;
        
        [XmlElement("interfaceRef", Order=0)]
        public List<XmlQualifiedName> InterfaceRef
        {
            get
            {
                return _interfaceRef;
            }
            set
            {
                if (_interfaceRef == value)
                    return;
                if (_interfaceRef == null || value == null || !_interfaceRef.SequenceEqual(value))
                {
                _interfaceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the InterfaceRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InterfaceRefSpecified
        {
            get
            {
                return (this.InterfaceRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Participant" /> class.</para>
        /// </summary>
        public Participant()
        {
            this._interfaceRef = new List<XmlQualifiedName>();
            this._endPointRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _endPointRef;
        
        [XmlElement("endPointRef", Order=1)]
        public List<XmlQualifiedName> EndPointRef
        {
            get
            {
                return _endPointRef;
            }
            set
            {
                if (_endPointRef == value)
                    return;
                if (_endPointRef == null || value == null || !_endPointRef.SequenceEqual(value))
                {
                _endPointRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EndPointRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EndPointRefSpecified
        {
            get
            {
                return (this.EndPointRef.Count != 0);
            }
        }
        
        [XmlIgnore]
        private ParticipantMultiplicity _participantMultiplicity;
        
        [XmlElement("participantMultiplicity", Order=2)]
        public ParticipantMultiplicity ParticipantMultiplicity
        {
            get
            {
                return _participantMultiplicity;
            }
            set
            {
                if (_participantMultiplicity == value)
                    return;
                if (_participantMultiplicity == null || value == null || !_participantMultiplicity.Equals(value))
                {
                _participantMultiplicity = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _processRef;
        
        [XmlAttribute("processRef")]
        public XmlQualifiedName ProcessRef
        {
            get
            {
                return _processRef;
            }
            set
            {
                if (_processRef == value)
                    return;
                if (_processRef == null || value == null || !_processRef.Equals(value))
                {
                _processRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tParticipantMultiplicity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("participantMultiplicity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ParticipantMultiplicity : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private int _minimum = 0;
        
        [DefaultValueAttribute(0)]
        [XmlAttribute("minimum")]
        public int Minimum
        {
            get
            {
                return _minimum;
            }
            set
            {
                if (!_minimum.Equals(value))
                {
                _minimum = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private int _maximum = 1;
        
        [DefaultValueAttribute(1)]
        [XmlAttribute("maximum")]
        public int Maximum
        {
            get
            {
                return _maximum;
            }
            set
            {
                if (!_maximum.Equals(value))
                {
                _maximum = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tMessageFlow", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("messageFlow", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class MessageFlow : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _sourceRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("sourceRef")]
        public XmlQualifiedName SourceRef
        {
            get
            {
                return _sourceRef;
            }
            set
            {
                if (_sourceRef == value)
                    return;
                if (_sourceRef == null || value == null || !_sourceRef.Equals(value))
                {
                _sourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _targetRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("targetRef")]
        public XmlQualifiedName TargetRef
        {
            get
            {
                return _targetRef;
            }
            set
            {
                if (_targetRef == value)
                    return;
                if (_targetRef == null || value == null || !_targetRef.Equals(value))
                {
                _targetRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _messageRef;
        
        [XmlAttribute("messageRef")]
        public XmlQualifiedName MessageRef
        {
            get
            {
                return _messageRef;
            }
            set
            {
                if (_messageRef == value)
                    return;
                if (_messageRef == null || value == null || !_messageRef.Equals(value))
                {
                _messageRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tConversationAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conversationAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ConversationAssociation : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _innerConversationNodeRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("innerConversationNodeRef")]
        public XmlQualifiedName InnerConversationNodeRef
        {
            get
            {
                return _innerConversationNodeRef;
            }
            set
            {
                if (_innerConversationNodeRef == value)
                    return;
                if (_innerConversationNodeRef == null || value == null || !_innerConversationNodeRef.Equals(value))
                {
                _innerConversationNodeRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _outerConversationNodeRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("outerConversationNodeRef")]
        public XmlQualifiedName OuterConversationNodeRef
        {
            get
            {
                return _outerConversationNodeRef;
            }
            set
            {
                if (_outerConversationNodeRef == value)
                    return;
                if (_outerConversationNodeRef == null || value == null || !_outerConversationNodeRef.Equals(value))
                {
                _outerConversationNodeRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tMessageFlowAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("messageFlowAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class MessageFlowAssociation : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _innerMessageFlowRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("innerMessageFlowRef")]
        public XmlQualifiedName InnerMessageFlowRef
        {
            get
            {
                return _innerMessageFlowRef;
            }
            set
            {
                if (_innerMessageFlowRef == value)
                    return;
                if (_innerMessageFlowRef == null || value == null || !_innerMessageFlowRef.Equals(value))
                {
                _innerMessageFlowRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _outerMessageFlowRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("outerMessageFlowRef")]
        public XmlQualifiedName OuterMessageFlowRef
        {
            get
            {
                return _outerMessageFlowRef;
            }
            set
            {
                if (_outerMessageFlowRef == value)
                    return;
                if (_outerMessageFlowRef == null || value == null || !_outerMessageFlowRef.Equals(value))
                {
                _outerMessageFlowRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tConversationLink", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conversationLink", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ConversationLink : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _sourceRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("sourceRef")]
        public XmlQualifiedName SourceRef
        {
            get
            {
                return _sourceRef;
            }
            set
            {
                if (_sourceRef == value)
                    return;
                if (_sourceRef == null || value == null || !_sourceRef.Equals(value))
                {
                _sourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _targetRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("targetRef")]
        public XmlQualifiedName TargetRef
        {
            get
            {
                return _targetRef;
            }
            set
            {
                if (_targetRef == value)
                    return;
                if (_targetRef == null || value == null || !_targetRef.Equals(value))
                {
                _targetRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tChoreographyTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("choreographyTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ChoreographyTask : ChoreographyActivity, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _messageFlowRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("messageFlowRef", Order=0)]
        public List<XmlQualifiedName> MessageFlowRef
        {
            get
            {
                return _messageFlowRef;
            }
            set
            {
                if (_messageFlowRef == value)
                    return;
                if (_messageFlowRef == null || value == null || !_messageFlowRef.SequenceEqual(value))
                {
                _messageFlowRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ChoreographyTask" /> class.</para>
        /// </summary>
        public ChoreographyTask()
        {
            this._messageFlowRef = new List<XmlQualifiedName>();
        }
    }
    
    
    [Serializable]
    [XmlType("tCompensateEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("compensateEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CompensateEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private bool _waitForCompletion;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("waitForCompletion")]
        public bool WaitForCompletionValue
        {
            get
            {
                return _waitForCompletion;
            }
            set
            {
                if (!_waitForCompletion.Equals(value))
                {
                _waitForCompletion = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the WaitForCompletion property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool WaitForCompletionValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> WaitForCompletion
        {
            get
            {
                if (this.WaitForCompletionValueSpecified)
                {
                    return this.WaitForCompletionValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.WaitForCompletionValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.WaitForCompletionValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.WaitForCompletionValue = value.GetValueOrDefault();
                    this.WaitForCompletionValueSpecified = value.HasValue;
                    OnPropertyChanged("WaitForCompletion");
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _activityRef;
        
        [XmlAttribute("activityRef")]
        public XmlQualifiedName ActivityRef
        {
            get
            {
                return _activityRef;
            }
            set
            {
                if (_activityRef == value)
                    return;
                if (_activityRef == null || value == null || !_activityRef.Equals(value))
                {
                _activityRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tComplexBehaviorDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("complexBehaviorDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ComplexBehaviorDefinition : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private FormalExpression _condition;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("condition", Order=0)]
        public FormalExpression Condition
        {
            get
            {
                return _condition;
            }
            set
            {
                if (_condition == value)
                    return;
                if (_condition == null || value == null || !_condition.Equals(value))
                {
                _condition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private ImplicitThrowEvent _event;
        
        [XmlElement("event", Order=1)]
        public ImplicitThrowEvent Event
        {
            get
            {
                return _event;
            }
            set
            {
                if (_event == value)
                    return;
                if (_event == null || value == null || !_event.Equals(value))
                {
                _event = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tImplicitThrowEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("implicitThrowEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ImplicitThrowEvent : ThrowEvent, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tThrowEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("throwEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(EndEvent))]
    [XmlInclude(typeof(ImplicitThrowEvent))]
    [XmlInclude(typeof(IntermediateThrowEvent))]
    public abstract partial class ThrowEvent : Event, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<DataInput> _dataInput;
        
        [XmlElement("dataInput", Order=0)]
        public List<DataInput> DataInput
        {
            get
            {
                return _dataInput;
            }
            set
            {
                if (_dataInput == value)
                    return;
                if (_dataInput == null || value == null || !_dataInput.SequenceEqual(value))
                {
                _dataInput = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataInput collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataInputSpecified
        {
            get
            {
                return (this.DataInput.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="ThrowEvent" /> class.</para>
        /// </summary>
        public ThrowEvent()
        {
            this._dataInput = new List<DataInput>();
            this._dataInputAssociation = new List<DataInputAssociation>();
            this._eventDefinition = new List<EventDefinition>();
            this._eventDefinitionRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<DataInputAssociation> _dataInputAssociation;
        
        [XmlElement("dataInputAssociation", Order=1)]
        public List<DataInputAssociation> DataInputAssociation
        {
            get
            {
                return _dataInputAssociation;
            }
            set
            {
                if (_dataInputAssociation == value)
                    return;
                if (_dataInputAssociation == null || value == null || !_dataInputAssociation.SequenceEqual(value))
                {
                _dataInputAssociation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the DataInputAssociation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DataInputAssociationSpecified
        {
            get
            {
                return (this.DataInputAssociation.Count != 0);
            }
        }
        
        [XmlIgnore]
        private InputSet _inputSet;
        
        [XmlElement("inputSet", Order=2)]
        public InputSet InputSet
        {
            get
            {
                return _inputSet;
            }
            set
            {
                if (_inputSet == value)
                    return;
                if (_inputSet == null || value == null || !_inputSet.Equals(value))
                {
                _inputSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<EventDefinition> _eventDefinition;
        
        [XmlElement("cancelEventDefinition", Type=typeof(CancelEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("compensateEventDefinition", Type=typeof(CompensateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("conditionalEventDefinition", Type=typeof(ConditionalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("errorEventDefinition", Type=typeof(ErrorEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("escalationEventDefinition", Type=typeof(EscalationEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("linkEventDefinition", Type=typeof(LinkEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("messageEventDefinition", Type=typeof(MessageEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("signalEventDefinition", Type=typeof(SignalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("terminateEventDefinition", Type=typeof(TerminateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("timerEventDefinition", Type=typeof(TimerEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=3)]
        [XmlElement("eventDefinition", Order=3)]
        public List<EventDefinition> EventDefinition
        {
            get
            {
                return _eventDefinition;
            }
            set
            {
                if (_eventDefinition == value)
                    return;
                if (_eventDefinition == null || value == null || !_eventDefinition.SequenceEqual(value))
                {
                _eventDefinition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EventDefinition collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EventDefinitionSpecified
        {
            get
            {
                return (this.EventDefinition.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _eventDefinitionRef;
        
        [XmlElement("eventDefinitionRef", Order=4)]
        public List<XmlQualifiedName> EventDefinitionRef
        {
            get
            {
                return _eventDefinitionRef;
            }
            set
            {
                if (_eventDefinitionRef == value)
                    return;
                if (_eventDefinitionRef == null || value == null || !_eventDefinitionRef.SequenceEqual(value))
                {
                _eventDefinitionRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EventDefinitionRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EventDefinitionRefSpecified
        {
            get
            {
                return (this.EventDefinitionRef.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tComplexGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("complexGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ComplexGateway : Gateway, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _activationCondition;
        
        [XmlElement("activationCondition", Order=0)]
        public Expression ActivationCondition
        {
            get
            {
                return _activationCondition;
            }
            set
            {
                if (_activationCondition == value)
                    return;
                if (_activationCondition == null || value == null || !_activationCondition.Equals(value))
                {
                _activationCondition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _default;
        
        [XmlAttribute("default")]
        public string Default
        {
            get
            {
                return _default;
            }
            set
            {
                if (_default == value)
                    return;
                if (_default == null || value == null || !_default.Equals(value))
                {
                _default = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("gateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(ComplexGateway))]
    [XmlInclude(typeof(EventBasedGateway))]
    [XmlInclude(typeof(ExclusiveGateway))]
    [XmlInclude(typeof(InclusiveGateway))]
    [XmlInclude(typeof(ParallelGateway))]
    public partial class Gateway : FlowNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private GatewayDirection _gatewayDirection = GatewayDirection.Unspecified;
        
        [DefaultValueAttribute(GatewayDirection.Unspecified)]
        [XmlAttribute("gatewayDirection")]
        public GatewayDirection GatewayDirection
        {
            get
            {
                return _gatewayDirection;
            }
            set
            {
                if (!_gatewayDirection.Equals(value))
                {
                _gatewayDirection = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tGatewayDirection", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum GatewayDirection
    {
        
        Unspecified,
        
        Converging,
        
        Diverging,
        
        Mixed,
    }
    
    
    [Serializable]
    [XmlType("tConditionalEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conditionalEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ConditionalEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _condition;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("condition", Order=0)]
        public Expression Condition
        {
            get
            {
                return _condition;
            }
            set
            {
                if (_condition == value)
                    return;
                if (_condition == null || value == null || !_condition.Equals(value))
                {
                _condition = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("conversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Conversation : ConversationNode, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tCorrelationProperty", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("correlationProperty", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CorrelationProperty : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<CorrelationPropertyRetrievalExpression> _correlationPropertyRetrievalExpression;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("correlationPropertyRetrievalExpression", Order=0)]
        public List<CorrelationPropertyRetrievalExpression> CorrelationPropertyRetrievalExpression
        {
            get
            {
                return _correlationPropertyRetrievalExpression;
            }
            set
            {
                if (_correlationPropertyRetrievalExpression == value)
                    return;
                if (_correlationPropertyRetrievalExpression == null || value == null || !_correlationPropertyRetrievalExpression.SequenceEqual(value))
                {
                _correlationPropertyRetrievalExpression = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CorrelationProperty" /> class.</para>
        /// </summary>
        public CorrelationProperty()
        {
            this._correlationPropertyRetrievalExpression = new List<CorrelationPropertyRetrievalExpression>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _type;
        
        [XmlAttribute("type")]
        public XmlQualifiedName Type
        {
            get
            {
                return _type;
            }
            set
            {
                if (_type == value)
                    return;
                if (_type == null || value == null || !_type.Equals(value))
                {
                _type = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCorrelationPropertyRetrievalExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("correlationPropertyRetrievalExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CorrelationPropertyRetrievalExpression : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private FormalExpression _messagePath;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("messagePath", Order=0)]
        public FormalExpression MessagePath
        {
            get
            {
                return _messagePath;
            }
            set
            {
                if (_messagePath == value)
                    return;
                if (_messagePath == null || value == null || !_messagePath.Equals(value))
                {
                _messagePath = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _messageRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("messageRef")]
        public XmlQualifiedName MessageRef
        {
            get
            {
                return _messageRef;
            }
            set
            {
                if (_messageRef == value)
                    return;
                if (_messageRef == null || value == null || !_messageRef.Equals(value))
                {
                _messageRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCorrelationPropertyBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("correlationPropertyBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CorrelationPropertyBinding : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private FormalExpression _dataPath;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("dataPath", Order=0)]
        public FormalExpression DataPath
        {
            get
            {
                return _dataPath;
            }
            set
            {
                if (_dataPath == value)
                    return;
                if (_dataPath == null || value == null || !_dataPath.Equals(value))
                {
                _dataPath = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _correlationPropertyRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("correlationPropertyRef")]
        public XmlQualifiedName CorrelationPropertyRef
        {
            get
            {
                return _correlationPropertyRef;
            }
            set
            {
                if (_correlationPropertyRef == value)
                    return;
                if (_correlationPropertyRef == null || value == null || !_correlationPropertyRef.Equals(value))
                {
                _correlationPropertyRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tCorrelationSubscription", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("correlationSubscription", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class CorrelationSubscription : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<CorrelationPropertyBinding> _correlationPropertyBinding;
        
        [XmlElement("correlationPropertyBinding", Order=0)]
        public List<CorrelationPropertyBinding> CorrelationPropertyBinding
        {
            get
            {
                return _correlationPropertyBinding;
            }
            set
            {
                if (_correlationPropertyBinding == value)
                    return;
                if (_correlationPropertyBinding == null || value == null || !_correlationPropertyBinding.SequenceEqual(value))
                {
                _correlationPropertyBinding = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationPropertyBinding collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationPropertyBindingSpecified
        {
            get
            {
                return (this.CorrelationPropertyBinding.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="CorrelationSubscription" /> class.</para>
        /// </summary>
        public CorrelationSubscription()
        {
            this._correlationPropertyBinding = new List<CorrelationPropertyBinding>();
        }
        
        [XmlIgnore]
        private XmlQualifiedName _correlationKeyRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("correlationKeyRef")]
        public XmlQualifiedName CorrelationKeyRef
        {
            get
            {
                return _correlationKeyRef;
            }
            set
            {
                if (_correlationKeyRef == value)
                    return;
                if (_correlationKeyRef == null || value == null || !_correlationKeyRef.Equals(value))
                {
                _correlationKeyRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataObject", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataObject", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataObject : FlowElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isCollection = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isCollection")]
        public bool IsCollection
        {
            get
            {
                return _isCollection;
            }
            set
            {
                if (!_isCollection.Equals(value))
                {
                _isCollection = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataObjectReference", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataObjectReference", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataObjectReference : FlowElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _dataObjectRef;
        
        [XmlAttribute("dataObjectRef")]
        public string DataObjectRef
        {
            get
            {
                return _dataObjectRef;
            }
            set
            {
                if (_dataObjectRef == value)
                    return;
                if (_dataObjectRef == null || value == null || !_dataObjectRef.Equals(value))
                {
                _dataObjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataStore", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataStore", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataStore : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _capacity;
        
        [XmlAttribute("capacity")]
        public string Capacity
        {
            get
            {
                return _capacity;
            }
            set
            {
                if (_capacity == value)
                    return;
                if (_capacity == null || value == null || !_capacity.Equals(value))
                {
                _capacity = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isUnlimited = true;
        
        [DefaultValueAttribute(true)]
        [XmlAttribute("isUnlimited")]
        public bool IsUnlimited
        {
            get
            {
                return _isUnlimited;
            }
            set
            {
                if (!_isUnlimited.Equals(value))
                {
                _isUnlimited = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDataStoreReference", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("dataStoreReference", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class DataStoreReference : FlowElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private DataState _dataState;
        
        [XmlElement("dataState", Order=0)]
        public DataState DataState
        {
            get
            {
                return _dataState;
            }
            set
            {
                if (_dataState == value)
                    return;
                if (_dataState == null || value == null || !_dataState.Equals(value))
                {
                _dataState = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemSubjectRef;
        
        [XmlAttribute("itemSubjectRef")]
        public XmlQualifiedName ItemSubjectRef
        {
            get
            {
                return _itemSubjectRef;
            }
            set
            {
                if (_itemSubjectRef == value)
                    return;
                if (_itemSubjectRef == null || value == null || !_itemSubjectRef.Equals(value))
                {
                _itemSubjectRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _dataStoreRef;
        
        [XmlAttribute("dataStoreRef")]
        public XmlQualifiedName DataStoreRef
        {
            get
            {
                return _dataStoreRef;
            }
            set
            {
                if (_dataStoreRef == value)
                    return;
                if (_dataStoreRef == null || value == null || !_dataStoreRef.Equals(value))
                {
                _dataStoreRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEndEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("endEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class EndEvent : ThrowEvent, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tEndPoint", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("endPoint", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class EndPoint : RootElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tError", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("error", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Error : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _errorCode;
        
        [XmlAttribute("errorCode")]
        public string ErrorCode
        {
            get
            {
                return _errorCode;
            }
            set
            {
                if (_errorCode == value)
                    return;
                if (_errorCode == null || value == null || !_errorCode.Equals(value))
                {
                _errorCode = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _structureRef;
        
        [XmlAttribute("structureRef")]
        public XmlQualifiedName StructureRef
        {
            get
            {
                return _structureRef;
            }
            set
            {
                if (_structureRef == value)
                    return;
                if (_structureRef == null || value == null || !_structureRef.Equals(value))
                {
                _structureRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tErrorEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("errorEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ErrorEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _errorRef;
        
        [XmlAttribute("errorRef")]
        public XmlQualifiedName ErrorRef
        {
            get
            {
                return _errorRef;
            }
            set
            {
                if (_errorRef == value)
                    return;
                if (_errorRef == null || value == null || !_errorRef.Equals(value))
                {
                _errorRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEscalation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("escalation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Escalation : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _escalationCode;
        
        [XmlAttribute("escalationCode")]
        public string EscalationCode
        {
            get
            {
                return _escalationCode;
            }
            set
            {
                if (_escalationCode == value)
                    return;
                if (_escalationCode == null || value == null || !_escalationCode.Equals(value))
                {
                _escalationCode = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _structureRef;
        
        [XmlAttribute("structureRef")]
        public XmlQualifiedName StructureRef
        {
            get
            {
                return _structureRef;
            }
            set
            {
                if (_structureRef == value)
                    return;
                if (_structureRef == null || value == null || !_structureRef.Equals(value))
                {
                _structureRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEscalationEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("escalationEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class EscalationEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _escalationRef;
        
        [XmlAttribute("escalationRef")]
        public XmlQualifiedName EscalationRef
        {
            get
            {
                return _escalationRef;
            }
            set
            {
                if (_escalationRef == value)
                    return;
                if (_escalationRef == null || value == null || !_escalationRef.Equals(value))
                {
                _escalationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEventBasedGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("eventBasedGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class EventBasedGateway : Gateway, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private bool _instantiate = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("instantiate")]
        public bool Instantiate
        {
            get
            {
                return _instantiate;
            }
            set
            {
                if (!_instantiate.Equals(value))
                {
                _instantiate = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private EventBasedGatewayType _eventGatewayType = EventBasedGatewayType.Exclusive;
        
        [DefaultValueAttribute(EventBasedGatewayType.Exclusive)]
        [XmlAttribute("eventGatewayType")]
        public EventBasedGatewayType EventGatewayType
        {
            get
            {
                return _eventGatewayType;
            }
            set
            {
                if (!_eventGatewayType.Equals(value))
                {
                _eventGatewayType = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tEventBasedGatewayType", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum EventBasedGatewayType
    {
        
        Exclusive,
        
        Parallel,
    }
    
    
    [Serializable]
    [XmlType("tExclusiveGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("exclusiveGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ExclusiveGateway : Gateway, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _default;
        
        [XmlAttribute("default")]
        public string Default
        {
            get
            {
                return _default;
            }
            set
            {
                if (_default == value)
                    return;
                if (_default == null || value == null || !_default.Equals(value))
                {
                _default = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tExtension", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("extension", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Extension : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<Documentation> _documentation;
        
        [XmlElement("documentation", Order=0)]
        public List<Documentation> Documentation
        {
            get
            {
                return _documentation;
            }
            set
            {
                if (_documentation == value)
                    return;
                if (_documentation == null || value == null || !_documentation.SequenceEqual(value))
                {
                _documentation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Documentation collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DocumentationSpecified
        {
            get
            {
                return (this.Documentation.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Extension" /> class.</para>
        /// </summary>
        public Extension()
        {
            this._documentation = new List<Documentation>();
        }
        
        [XmlIgnore]
        private XmlQualifiedName _definition;
        
        [XmlAttribute("definition")]
        public XmlQualifiedName Definition
        {
            get
            {
                return _definition;
            }
            set
            {
                if (_definition == value)
                    return;
                if (_definition == null || value == null || !_definition.Equals(value))
                {
                _definition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _mustUnderstand = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("mustUnderstand")]
        public bool MustUnderstand
        {
            get
            {
                return _mustUnderstand;
            }
            set
            {
                if (!_mustUnderstand.Equals(value))
                {
                _mustUnderstand = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tGlobalBusinessRuleTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalBusinessRuleTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalBusinessRuleTask : GlobalTask, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _implementation = "##unspecified";
        
        [DefaultValueAttribute("##unspecified")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tGlobalTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(GlobalBusinessRuleTask))]
    [XmlInclude(typeof(GlobalManualTask))]
    [XmlInclude(typeof(GlobalScriptTask))]
    [XmlInclude(typeof(GlobalUserTask))]
    public partial class GlobalTask : CallableElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<ResourceRole> _resourceRole;
        
        [XmlElement("performer", Type=typeof(Performer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("humanPerformer", Type=typeof(HumanPerformer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("potentialOwner", Type=typeof(PotentialOwner), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("resourceRole", Order=0)]
        public List<ResourceRole> ResourceRole
        {
            get
            {
                return _resourceRole;
            }
            set
            {
                if (_resourceRole == value)
                    return;
                if (_resourceRole == null || value == null || !_resourceRole.SequenceEqual(value))
                {
                _resourceRole = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ResourceRole collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ResourceRoleSpecified
        {
            get
            {
                return (this.ResourceRole.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="GlobalTask" /> class.</para>
        /// </summary>
        public GlobalTask()
        {
            this._resourceRole = new List<ResourceRole>();
        }
    }
    
    
    [Serializable]
    [XmlType("tGlobalChoreographyTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalChoreographyTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalChoreographyTask : Choreography, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _initiatingParticipantRef;
        
        [XmlAttribute("initiatingParticipantRef")]
        public XmlQualifiedName InitiatingParticipantRef
        {
            get
            {
                return _initiatingParticipantRef;
            }
            set
            {
                if (_initiatingParticipantRef == value)
                    return;
                if (_initiatingParticipantRef == null || value == null || !_initiatingParticipantRef.Equals(value))
                {
                _initiatingParticipantRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tGlobalConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalConversation : Collaboration, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tGlobalManualTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalManualTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalManualTask : GlobalTask, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tGlobalScriptTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalScriptTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalScriptTask : GlobalTask, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Script _script;
        
        [XmlElement("script", Order=0)]
        public Script Script
        {
            get
            {
                return _script;
            }
            set
            {
                if (_script == value)
                    return;
                if (_script == null || value == null || !_script.Equals(value))
                {
                _script = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _scriptLanguage;
        
        [XmlAttribute("scriptLanguage")]
        public string ScriptLanguage
        {
            get
            {
                return _scriptLanguage;
            }
            set
            {
                if (_scriptLanguage == value)
                    return;
                if (_scriptLanguage == null || value == null || !_scriptLanguage.Equals(value))
                {
                _scriptLanguage = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tScript", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("script", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Script : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private XmlElement _any;
        
        [XmlAnyElementAttribute(Order=0)]
        public XmlElement Any
        {
            get
            {
                return _any;
            }
            set
            {
                if (_any == value)
                    return;
                if (_any == null || value == null || !_any.Equals(value))
                {
                _any = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlTextAttribute()]
        public string[] Text { get; set; }
    }
    
    
    [Serializable]
    [XmlType("tGlobalUserTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("globalUserTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class GlobalUserTask : GlobalTask, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Rendering> _rendering;
        
        [XmlElement("rendering", Order=0)]
        public List<Rendering> Rendering
        {
            get
            {
                return _rendering;
            }
            set
            {
                if (_rendering == value)
                    return;
                if (_rendering == null || value == null || !_rendering.SequenceEqual(value))
                {
                _rendering = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Rendering collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool RenderingSpecified
        {
            get
            {
                return (this.Rendering.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="GlobalUserTask" /> class.</para>
        /// </summary>
        public GlobalUserTask()
        {
            this._rendering = new List<Rendering>();
        }
        
        [XmlIgnore]
        private string _implementation = "##unspecified";
        
        [DefaultValueAttribute("##unspecified")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tRendering", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("rendering", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Rendering : BaseElement, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tGroup", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("group", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Group : Artifact, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _categoryValueRef;
        
        [XmlAttribute("categoryValueRef")]
        public XmlQualifiedName CategoryValueRef
        {
            get
            {
                return _categoryValueRef;
            }
            set
            {
                if (_categoryValueRef == value)
                    return;
                if (_categoryValueRef == null || value == null || !_categoryValueRef.Equals(value))
                {
                _categoryValueRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tHumanPerformer", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("humanPerformer", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(PotentialOwner))]
    public partial class HumanPerformer : Performer, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tPerformer", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("performer", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [XmlInclude(typeof(HumanPerformer))]
    [XmlInclude(typeof(PotentialOwner))]
    public partial class Performer : ResourceRole, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tInclusiveGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("inclusiveGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class InclusiveGateway : Gateway, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _default;
        
        [XmlAttribute("default")]
        public string Default
        {
            get
            {
                return _default;
            }
            set
            {
                if (_default == value)
                    return;
                if (_default == null || value == null || !_default.Equals(value))
                {
                _default = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tInterface", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("interface", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Interface : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Operation> _operation;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("operation", Order=0)]
        public List<Operation> Operation
        {
            get
            {
                return _operation;
            }
            set
            {
                if (_operation == value)
                    return;
                if (_operation == null || value == null || !_operation.SequenceEqual(value))
                {
                _operation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Interface" /> class.</para>
        /// </summary>
        public Interface()
        {
            this._operation = new List<Operation>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _implementationRef;
        
        [XmlAttribute("implementationRef")]
        public XmlQualifiedName ImplementationRef
        {
            get
            {
                return _implementationRef;
            }
            set
            {
                if (_implementationRef == value)
                    return;
                if (_implementationRef == null || value == null || !_implementationRef.Equals(value))
                {
                _implementationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tOperation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("operation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Operation : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _inMessageRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("inMessageRef", Order=0)]
        public XmlQualifiedName InMessageRef
        {
            get
            {
                return _inMessageRef;
            }
            set
            {
                if (_inMessageRef == value)
                    return;
                if (_inMessageRef == null || value == null || !_inMessageRef.Equals(value))
                {
                _inMessageRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _outMessageRef;
        
        [XmlElement("outMessageRef", Order=1)]
        public XmlQualifiedName OutMessageRef
        {
            get
            {
                return _outMessageRef;
            }
            set
            {
                if (_outMessageRef == value)
                    return;
                if (_outMessageRef == null || value == null || !_outMessageRef.Equals(value))
                {
                _outMessageRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _errorRef;
        
        [XmlElement("errorRef", Order=2)]
        public List<XmlQualifiedName> ErrorRef
        {
            get
            {
                return _errorRef;
            }
            set
            {
                if (_errorRef == value)
                    return;
                if (_errorRef == null || value == null || !_errorRef.SequenceEqual(value))
                {
                _errorRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ErrorRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ErrorRefSpecified
        {
            get
            {
                return (this.ErrorRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Operation" /> class.</para>
        /// </summary>
        public Operation()
        {
            this._errorRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _implementationRef;
        
        [XmlAttribute("implementationRef")]
        public XmlQualifiedName ImplementationRef
        {
            get
            {
                return _implementationRef;
            }
            set
            {
                if (_implementationRef == value)
                    return;
                if (_implementationRef == null || value == null || !_implementationRef.Equals(value))
                {
                _implementationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tIntermediateCatchEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("intermediateCatchEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class IntermediateCatchEvent : CatchEvent, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tIntermediateThrowEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("intermediateThrowEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class IntermediateThrowEvent : ThrowEvent, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tItemDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("itemDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ItemDefinition : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _structureRef;
        
        [XmlAttribute("structureRef")]
        public XmlQualifiedName StructureRef
        {
            get
            {
                return _structureRef;
            }
            set
            {
                if (_structureRef == value)
                    return;
                if (_structureRef == null || value == null || !_structureRef.Equals(value))
                {
                _structureRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isCollection = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isCollection")]
        public bool IsCollection
        {
            get
            {
                return _isCollection;
            }
            set
            {
                if (!_isCollection.Equals(value))
                {
                _isCollection = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private ItemKind _itemKind = ItemKind.Information;
        
        [DefaultValueAttribute(ItemKind.Information)]
        [XmlAttribute("itemKind")]
        public ItemKind ItemKind
        {
            get
            {
                return _itemKind;
            }
            set
            {
                if (!_itemKind.Equals(value))
                {
                _itemKind = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tItemKind", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum ItemKind
    {
        
        Information,
        
        Physical,
    }
    
    
    [Serializable]
    [XmlType("tLinkEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("linkEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class LinkEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _source;
        
        [XmlElement("source", Order=0)]
        public List<XmlQualifiedName> Source
        {
            get
            {
                return _source;
            }
            set
            {
                if (_source == value)
                    return;
                if (_source == null || value == null || !_source.SequenceEqual(value))
                {
                _source = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Source collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool SourceSpecified
        {
            get
            {
                return (this.Source.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="LinkEventDefinition" /> class.</para>
        /// </summary>
        public LinkEventDefinition()
        {
            this._source = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private XmlQualifiedName _target;
        
        [XmlElement("target", Order=1)]
        public XmlQualifiedName Target
        {
            get
            {
                return _target;
            }
            set
            {
                if (_target == value)
                    return;
                if (_target == null || value == null || !_target.Equals(value))
                {
                _target = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tManualTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("manualTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ManualTask : Task, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tMessage", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("message", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Message : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _itemRef;
        
        [XmlAttribute("itemRef")]
        public XmlQualifiedName ItemRef
        {
            get
            {
                return _itemRef;
            }
            set
            {
                if (_itemRef == value)
                    return;
                if (_itemRef == null || value == null || !_itemRef.Equals(value))
                {
                _itemRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tMessageEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("messageEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class MessageEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _operationRef;
        
        [XmlElement("operationRef", Order=0)]
        public XmlQualifiedName OperationRef
        {
            get
            {
                return _operationRef;
            }
            set
            {
                if (_operationRef == value)
                    return;
                if (_operationRef == null || value == null || !_operationRef.Equals(value))
                {
                _operationRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _messageRef;
        
        [XmlAttribute("messageRef")]
        public XmlQualifiedName MessageRef
        {
            get
            {
                return _messageRef;
            }
            set
            {
                if (_messageRef == value)
                    return;
                if (_messageRef == null || value == null || !_messageRef.Equals(value))
                {
                _messageRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tMultiInstanceLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("multiInstanceLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class MultiInstanceLoopCharacteristics : LoopCharacteristics, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _loopCardinality;
        
        [XmlElement("loopCardinality", Order=0)]
        public Expression LoopCardinality
        {
            get
            {
                return _loopCardinality;
            }
            set
            {
                if (_loopCardinality == value)
                    return;
                if (_loopCardinality == null || value == null || !_loopCardinality.Equals(value))
                {
                _loopCardinality = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _loopDataInputRef;
        
        [XmlElement("loopDataInputRef", Order=1)]
        public XmlQualifiedName LoopDataInputRef
        {
            get
            {
                return _loopDataInputRef;
            }
            set
            {
                if (_loopDataInputRef == value)
                    return;
                if (_loopDataInputRef == null || value == null || !_loopDataInputRef.Equals(value))
                {
                _loopDataInputRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _loopDataOutputRef;
        
        [XmlElement("loopDataOutputRef", Order=2)]
        public XmlQualifiedName LoopDataOutputRef
        {
            get
            {
                return _loopDataOutputRef;
            }
            set
            {
                if (_loopDataOutputRef == value)
                    return;
                if (_loopDataOutputRef == null || value == null || !_loopDataOutputRef.Equals(value))
                {
                _loopDataOutputRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private DataInput _inputDataItem;
        
        [XmlElement("inputDataItem", Order=3)]
        public DataInput InputDataItem
        {
            get
            {
                return _inputDataItem;
            }
            set
            {
                if (_inputDataItem == value)
                    return;
                if (_inputDataItem == null || value == null || !_inputDataItem.Equals(value))
                {
                _inputDataItem = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private DataOutput _outputDataItem;
        
        [XmlElement("outputDataItem", Order=4)]
        public DataOutput OutputDataItem
        {
            get
            {
                return _outputDataItem;
            }
            set
            {
                if (_outputDataItem == value)
                    return;
                if (_outputDataItem == null || value == null || !_outputDataItem.Equals(value))
                {
                _outputDataItem = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<ComplexBehaviorDefinition> _complexBehaviorDefinition;
        
        [XmlElement("complexBehaviorDefinition", Order=5)]
        public List<ComplexBehaviorDefinition> ComplexBehaviorDefinition
        {
            get
            {
                return _complexBehaviorDefinition;
            }
            set
            {
                if (_complexBehaviorDefinition == value)
                    return;
                if (_complexBehaviorDefinition == null || value == null || !_complexBehaviorDefinition.SequenceEqual(value))
                {
                _complexBehaviorDefinition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ComplexBehaviorDefinition collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ComplexBehaviorDefinitionSpecified
        {
            get
            {
                return (this.ComplexBehaviorDefinition.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="MultiInstanceLoopCharacteristics" /> class.</para>
        /// </summary>
        public MultiInstanceLoopCharacteristics()
        {
            this._complexBehaviorDefinition = new List<ComplexBehaviorDefinition>();
        }
        
        [XmlIgnore]
        private Expression _completionCondition;
        
        [XmlElement("completionCondition", Order=6)]
        public Expression CompletionCondition
        {
            get
            {
                return _completionCondition;
            }
            set
            {
                if (_completionCondition == value)
                    return;
                if (_completionCondition == null || value == null || !_completionCondition.Equals(value))
                {
                _completionCondition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isSequential = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isSequential")]
        public bool IsSequential
        {
            get
            {
                return _isSequential;
            }
            set
            {
                if (!_isSequential.Equals(value))
                {
                _isSequential = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private MultiInstanceFlowCondition _behavior = MultiInstanceFlowCondition.All;
        
        [DefaultValueAttribute(MultiInstanceFlowCondition.All)]
        [XmlAttribute("behavior")]
        public MultiInstanceFlowCondition Behavior
        {
            get
            {
                return _behavior;
            }
            set
            {
                if (!_behavior.Equals(value))
                {
                _behavior = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _oneBehaviorEventRef;
        
        [XmlAttribute("oneBehaviorEventRef")]
        public XmlQualifiedName OneBehaviorEventRef
        {
            get
            {
                return _oneBehaviorEventRef;
            }
            set
            {
                if (_oneBehaviorEventRef == value)
                    return;
                if (_oneBehaviorEventRef == null || value == null || !_oneBehaviorEventRef.Equals(value))
                {
                _oneBehaviorEventRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _noneBehaviorEventRef;
        
        [XmlAttribute("noneBehaviorEventRef")]
        public XmlQualifiedName NoneBehaviorEventRef
        {
            get
            {
                return _noneBehaviorEventRef;
            }
            set
            {
                if (_noneBehaviorEventRef == value)
                    return;
                if (_noneBehaviorEventRef == null || value == null || !_noneBehaviorEventRef.Equals(value))
                {
                _noneBehaviorEventRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tMultiInstanceFlowCondition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum MultiInstanceFlowCondition
    {
        
        None,
        
        One,
        
        All,
        
        Complex,
    }
    
    
    [Serializable]
    [XmlType("tParallelGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("parallelGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ParallelGateway : Gateway, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tPartnerEntity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("partnerEntity", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class PartnerEntity : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _participantRef;
        
        [XmlElement("participantRef", Order=0)]
        public List<XmlQualifiedName> ParticipantRef
        {
            get
            {
                return _participantRef;
            }
            set
            {
                if (_participantRef == value)
                    return;
                if (_participantRef == null || value == null || !_participantRef.SequenceEqual(value))
                {
                _participantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantRefSpecified
        {
            get
            {
                return (this.ParticipantRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="PartnerEntity" /> class.</para>
        /// </summary>
        public PartnerEntity()
        {
            this._participantRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tPartnerRole", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("partnerRole", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class PartnerRole : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _participantRef;
        
        [XmlElement("participantRef", Order=0)]
        public List<XmlQualifiedName> ParticipantRef
        {
            get
            {
                return _participantRef;
            }
            set
            {
                if (_participantRef == value)
                    return;
                if (_participantRef == null || value == null || !_participantRef.SequenceEqual(value))
                {
                _participantRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParticipantRef collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParticipantRefSpecified
        {
            get
            {
                return (this.ParticipantRef.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="PartnerRole" /> class.</para>
        /// </summary>
        public PartnerRole()
        {
            this._participantRef = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tPotentialOwner", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("potentialOwner", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class PotentialOwner : HumanPerformer, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tProcess", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("process", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Process : CallableElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Auditing _auditing;
        
        [XmlElement("auditing", Order=0)]
        public Auditing Auditing
        {
            get
            {
                return _auditing;
            }
            set
            {
                if (_auditing == value)
                    return;
                if (_auditing == null || value == null || !_auditing.Equals(value))
                {
                _auditing = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private Monitoring _monitoring;
        
        [XmlElement("monitoring", Order=1)]
        public Monitoring Monitoring
        {
            get
            {
                return _monitoring;
            }
            set
            {
                if (_monitoring == value)
                    return;
                if (_monitoring == null || value == null || !_monitoring.Equals(value))
                {
                _monitoring = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<Property> _property;
        
        [XmlElement("property", Order=2)]
        public List<Property> Property
        {
            get
            {
                return _property;
            }
            set
            {
                if (_property == value)
                    return;
                if (_property == null || value == null || !_property.SequenceEqual(value))
                {
                _property = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Property collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool PropertySpecified
        {
            get
            {
                return (this.Property.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Process" /> class.</para>
        /// </summary>
        public Process()
        {
            this._property = new List<Property>();
            this._laneSet = new List<LaneSet>();
            this._flowElement = new List<FlowElement>();
            this._artifact = new List<Artifact>();
            this._resourceRole = new List<ResourceRole>();
            this._correlationSubscription = new List<CorrelationSubscription>();
            this._supports = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<LaneSet> _laneSet;
        
        [XmlElement("laneSet", Order=3)]
        public List<LaneSet> LaneSet
        {
            get
            {
                return _laneSet;
            }
            set
            {
                if (_laneSet == value)
                    return;
                if (_laneSet == null || value == null || !_laneSet.SequenceEqual(value))
                {
                _laneSet = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the LaneSet collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool LaneSetSpecified
        {
            get
            {
                return (this.LaneSet.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<FlowElement> _flowElement;
        
        [XmlElement("adHocSubProcess", Type=typeof(AdHocSubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("boundaryEvent", Type=typeof(BoundaryEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("businessRuleTask", Type=typeof(BusinessRuleTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("callActivity", Type=typeof(CallActivity), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("callChoreography", Type=typeof(CallChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("choreographyTask", Type=typeof(ChoreographyTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("complexGateway", Type=typeof(ComplexGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("dataObject", Type=typeof(DataObject), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("dataObjectReference", Type=typeof(DataObjectReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("dataStoreReference", Type=typeof(DataStoreReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("endEvent", Type=typeof(EndEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("event", Type=typeof(Event), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("eventBasedGateway", Type=typeof(EventBasedGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("exclusiveGateway", Type=typeof(ExclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("implicitThrowEvent", Type=typeof(ImplicitThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("inclusiveGateway", Type=typeof(InclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("intermediateCatchEvent", Type=typeof(IntermediateCatchEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("intermediateThrowEvent", Type=typeof(IntermediateThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("manualTask", Type=typeof(ManualTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("parallelGateway", Type=typeof(ParallelGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("receiveTask", Type=typeof(ReceiveTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("scriptTask", Type=typeof(ScriptTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("sendTask", Type=typeof(SendTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("sequenceFlow", Type=typeof(SequenceFlow), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("serviceTask", Type=typeof(ServiceTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("startEvent", Type=typeof(StartEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("subChoreography", Type=typeof(SubChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("subProcess", Type=typeof(SubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("task", Type=typeof(Task), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("transaction", Type=typeof(Transaction), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("userTask", Type=typeof(UserTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=4)]
        [XmlElement("flowElement", Order=4)]
        public List<FlowElement> FlowElement
        {
            get
            {
                return _flowElement;
            }
            set
            {
                if (_flowElement == value)
                    return;
                if (_flowElement == null || value == null || !_flowElement.SequenceEqual(value))
                {
                _flowElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FlowElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool FlowElementSpecified
        {
            get
            {
                return (this.FlowElement.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<Artifact> _artifact;
        
        [XmlElement("association", Type=typeof(Association), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=5)]
        [XmlElement("group", Type=typeof(Group), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=5)]
        [XmlElement("textAnnotation", Type=typeof(TextAnnotation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=5)]
        [XmlElement("artifact", Order=5)]
        public List<Artifact> Artifact
        {
            get
            {
                return _artifact;
            }
            set
            {
                if (_artifact == value)
                    return;
                if (_artifact == null || value == null || !_artifact.SequenceEqual(value))
                {
                _artifact = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Artifact collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ArtifactSpecified
        {
            get
            {
                return (this.Artifact.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<ResourceRole> _resourceRole;
        
        [XmlElement("performer", Type=typeof(Performer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=6)]
        [XmlElement("humanPerformer", Type=typeof(HumanPerformer), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=6)]
        [XmlElement("potentialOwner", Type=typeof(PotentialOwner), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=6)]
        [XmlElement("resourceRole", Order=6)]
        public List<ResourceRole> ResourceRole
        {
            get
            {
                return _resourceRole;
            }
            set
            {
                if (_resourceRole == value)
                    return;
                if (_resourceRole == null || value == null || !_resourceRole.SequenceEqual(value))
                {
                _resourceRole = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ResourceRole collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ResourceRoleSpecified
        {
            get
            {
                return (this.ResourceRole.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<CorrelationSubscription> _correlationSubscription;
        
        [XmlElement("correlationSubscription", Order=7)]
        public List<CorrelationSubscription> CorrelationSubscription
        {
            get
            {
                return _correlationSubscription;
            }
            set
            {
                if (_correlationSubscription == value)
                    return;
                if (_correlationSubscription == null || value == null || !_correlationSubscription.SequenceEqual(value))
                {
                _correlationSubscription = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CorrelationSubscription collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CorrelationSubscriptionSpecified
        {
            get
            {
                return (this.CorrelationSubscription.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _supports;
        
        [XmlElement("supports", Order=8)]
        public List<XmlQualifiedName> Supports
        {
            get
            {
                return _supports;
            }
            set
            {
                if (_supports == value)
                    return;
                if (_supports == null || value == null || !_supports.SequenceEqual(value))
                {
                _supports = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Supports collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool SupportsSpecified
        {
            get
            {
                return (this.Supports.Count != 0);
            }
        }
        
        [XmlIgnore]
        private ProcessType _processType = ProcessType.None;
        
        [DefaultValueAttribute(ProcessType.None)]
        [XmlAttribute("processType")]
        public ProcessType ProcessType
        {
            get
            {
                return _processType;
            }
            set
            {
                if (!_processType.Equals(value))
                {
                _processType = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isClosed = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("isClosed")]
        public bool IsClosed
        {
            get
            {
                return _isClosed;
            }
            set
            {
                if (!_isClosed.Equals(value))
                {
                _isClosed = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isExecutable;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isExecutable")]
        public bool IsExecutableValue
        {
            get
            {
                return _isExecutable;
            }
            set
            {
                if (!_isExecutable.Equals(value))
                {
                _isExecutable = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsExecutable property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsExecutableValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsExecutable
        {
            get
            {
                if (this.IsExecutableValueSpecified)
                {
                    return this.IsExecutableValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsExecutableValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsExecutableValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsExecutableValue = value.GetValueOrDefault();
                    this.IsExecutableValueSpecified = value.HasValue;
                    OnPropertyChanged("IsExecutable");
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _definitionalCollaborationRef;
        
        [XmlAttribute("definitionalCollaborationRef")]
        public XmlQualifiedName DefinitionalCollaborationRef
        {
            get
            {
                return _definitionalCollaborationRef;
            }
            set
            {
                if (_definitionalCollaborationRef == value)
                    return;
                if (_definitionalCollaborationRef == null || value == null || !_definitionalCollaborationRef.Equals(value))
                {
                _definitionalCollaborationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tProcessType", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum ProcessType
    {
        
        None,
        
        Public,
        
        Private,
    }
    
    
    [Serializable]
    [XmlType("tReceiveTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("receiveTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ReceiveTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _implementation = "##WebService";
        
        [DefaultValueAttribute("##WebService")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _instantiate = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("instantiate")]
        public bool Instantiate
        {
            get
            {
                return _instantiate;
            }
            set
            {
                if (!_instantiate.Equals(value))
                {
                _instantiate = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _messageRef;
        
        [XmlAttribute("messageRef")]
        public XmlQualifiedName MessageRef
        {
            get
            {
                return _messageRef;
            }
            set
            {
                if (_messageRef == value)
                    return;
                if (_messageRef == null || value == null || !_messageRef.Equals(value))
                {
                _messageRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _operationRef;
        
        [XmlAttribute("operationRef")]
        public XmlQualifiedName OperationRef
        {
            get
            {
                return _operationRef;
            }
            set
            {
                if (_operationRef == value)
                    return;
                if (_operationRef == null || value == null || !_operationRef.Equals(value))
                {
                _operationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tRelationship", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("relationship", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Relationship : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<XmlQualifiedName> _source;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("source", Order=0)]
        public List<XmlQualifiedName> Source
        {
            get
            {
                return _source;
            }
            set
            {
                if (_source == value)
                    return;
                if (_source == null || value == null || !_source.SequenceEqual(value))
                {
                _source = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Relationship" /> class.</para>
        /// </summary>
        public Relationship()
        {
            this._source = new List<XmlQualifiedName>();
            this._target = new List<XmlQualifiedName>();
        }
        
        [XmlIgnore]
        private List<XmlQualifiedName> _target;
        
        [Required(AllowEmptyStrings=true)]
        [XmlElement("target", Order=1)]
        public List<XmlQualifiedName> Target
        {
            get
            {
                return _target;
            }
            set
            {
                if (_target == value)
                    return;
                if (_target == null || value == null || !_target.SequenceEqual(value))
                {
                _target = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _type;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("type")]
        public string Type
        {
            get
            {
                return _type;
            }
            set
            {
                if (_type == value)
                    return;
                if (_type == null || value == null || !_type.Equals(value))
                {
                _type = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private RelationshipDirection _direction;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("direction")]
        public RelationshipDirection DirectionValue
        {
            get
            {
                return _direction;
            }
            set
            {
                if (!_direction.Equals(value))
                {
                _direction = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Direction property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool DirectionValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<RelationshipDirection> Direction
        {
            get
            {
                if (this.DirectionValueSpecified)
                {
                    return this.DirectionValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.DirectionValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.DirectionValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.DirectionValue = value.GetValueOrDefault();
                    this.DirectionValueSpecified = value.HasValue;
                    OnPropertyChanged("Direction");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tRelationshipDirection", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public enum RelationshipDirection
    {
        
        None,
        
        Forward,
        
        Backward,
        
        Both,
    }
    
    
    [Serializable]
    [XmlType("tResource", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("resource", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Resource : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<ResourceParameter> _resourceParameter;
        
        [XmlElement("resourceParameter", Order=0)]
        public List<ResourceParameter> ResourceParameter
        {
            get
            {
                return _resourceParameter;
            }
            set
            {
                if (_resourceParameter == value)
                    return;
                if (_resourceParameter == null || value == null || !_resourceParameter.SequenceEqual(value))
                {
                _resourceParameter = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ResourceParameter collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ResourceParameterSpecified
        {
            get
            {
                return (this.ResourceParameter.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Resource" /> class.</para>
        /// </summary>
        public Resource()
        {
            this._resourceParameter = new List<ResourceParameter>();
        }
        
        [XmlIgnore]
        private string _name;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tResourceParameter", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("resourceParameter", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ResourceParameter : BaseElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _type;
        
        [XmlAttribute("type")]
        public XmlQualifiedName Type
        {
            get
            {
                return _type;
            }
            set
            {
                if (_type == value)
                    return;
                if (_type == null || value == null || !_type.Equals(value))
                {
                _type = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isRequired;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isRequired")]
        public bool IsRequiredValue
        {
            get
            {
                return _isRequired;
            }
            set
            {
                if (!_isRequired.Equals(value))
                {
                _isRequired = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsRequired property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsRequiredValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsRequired
        {
            get
            {
                if (this.IsRequiredValueSpecified)
                {
                    return this.IsRequiredValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsRequiredValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsRequiredValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsRequiredValue = value.GetValueOrDefault();
                    this.IsRequiredValueSpecified = value.HasValue;
                    OnPropertyChanged("IsRequired");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tScriptTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("scriptTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ScriptTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Script _script;
        
        [XmlElement("script", Order=0)]
        public Script Script
        {
            get
            {
                return _script;
            }
            set
            {
                if (_script == value)
                    return;
                if (_script == null || value == null || !_script.Equals(value))
                {
                _script = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _scriptFormat;
        
        [XmlAttribute("scriptFormat")]
        public string ScriptFormat
        {
            get
            {
                return _scriptFormat;
            }
            set
            {
                if (_scriptFormat == value)
                    return;
                if (_scriptFormat == null || value == null || !_scriptFormat.Equals(value))
                {
                _scriptFormat = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSendTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("sendTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class SendTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _implementation = "##WebService";
        
        [DefaultValueAttribute("##WebService")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _messageRef;
        
        [XmlAttribute("messageRef")]
        public XmlQualifiedName MessageRef
        {
            get
            {
                return _messageRef;
            }
            set
            {
                if (_messageRef == value)
                    return;
                if (_messageRef == null || value == null || !_messageRef.Equals(value))
                {
                _messageRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _operationRef;
        
        [XmlAttribute("operationRef")]
        public XmlQualifiedName OperationRef
        {
            get
            {
                return _operationRef;
            }
            set
            {
                if (_operationRef == value)
                    return;
                if (_operationRef == null || value == null || !_operationRef.Equals(value))
                {
                _operationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSequenceFlow", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("sequenceFlow", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class SequenceFlow : FlowElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _conditionExpression;
        
        [XmlElement("conditionExpression", Order=0)]
        public Expression ConditionExpression
        {
            get
            {
                return _conditionExpression;
            }
            set
            {
                if (_conditionExpression == value)
                    return;
                if (_conditionExpression == null || value == null || !_conditionExpression.Equals(value))
                {
                _conditionExpression = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _sourceRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("sourceRef")]
        public string SourceRef
        {
            get
            {
                return _sourceRef;
            }
            set
            {
                if (_sourceRef == value)
                    return;
                if (_sourceRef == null || value == null || !_sourceRef.Equals(value))
                {
                _sourceRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _targetRef;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("targetRef")]
        public string TargetRef
        {
            get
            {
                return _targetRef;
            }
            set
            {
                if (_targetRef == value)
                    return;
                if (_targetRef == null || value == null || !_targetRef.Equals(value))
                {
                _targetRef = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _isImmediate;
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("isImmediate")]
        public bool IsImmediateValue
        {
            get
            {
                return _isImmediate;
            }
            set
            {
                if (!_isImmediate.Equals(value))
                {
                _isImmediate = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsImmediate property is specified.</para>
        /// </summary>
        [XmlIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsImmediateValueSpecified { get; set; }
        
        [XmlIgnore]
        public System.Nullable<bool> IsImmediate
        {
            get
            {
                if (this.IsImmediateValueSpecified)
                {
                    return this.IsImmediateValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (((this.IsImmediateValue.Equals(value.GetValueOrDefault()) == false) 
                            || (this.IsImmediateValueSpecified.Equals(value.HasValue) == false)))
                {
                    this.IsImmediateValue = value.GetValueOrDefault();
                    this.IsImmediateValueSpecified = value.HasValue;
                    OnPropertyChanged("IsImmediate");
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tServiceTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("serviceTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class ServiceTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _implementation = "##WebService";
        
        [DefaultValueAttribute("##WebService")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _operationRef;
        
        [XmlAttribute("operationRef")]
        public XmlQualifiedName OperationRef
        {
            get
            {
                return _operationRef;
            }
            set
            {
                if (_operationRef == value)
                    return;
                if (_operationRef == null || value == null || !_operationRef.Equals(value))
                {
                _operationRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSignal", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("signal", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Signal : RootElement, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private XmlQualifiedName _structureRef;
        
        [XmlAttribute("structureRef")]
        public XmlQualifiedName StructureRef
        {
            get
            {
                return _structureRef;
            }
            set
            {
                if (_structureRef == value)
                    return;
                if (_structureRef == null || value == null || !_structureRef.Equals(value))
                {
                _structureRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSignalEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("signalEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class SignalEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private XmlQualifiedName _signalRef;
        
        [XmlAttribute("signalRef")]
        public XmlQualifiedName SignalRef
        {
            get
            {
                return _signalRef;
            }
            set
            {
                if (_signalRef == value)
                    return;
                if (_signalRef == null || value == null || !_signalRef.Equals(value))
                {
                _signalRef = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tStandardLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("standardLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class StandardLoopCharacteristics : LoopCharacteristics, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _loopCondition;
        
        [XmlElement("loopCondition", Order=0)]
        public Expression LoopCondition
        {
            get
            {
                return _loopCondition;
            }
            set
            {
                if (_loopCondition == value)
                    return;
                if (_loopCondition == null || value == null || !_loopCondition.Equals(value))
                {
                _loopCondition = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private bool _testBefore = false;
        
        [DefaultValueAttribute(false)]
        [XmlAttribute("testBefore")]
        public bool TestBefore
        {
            get
            {
                return _testBefore;
            }
            set
            {
                if (!_testBefore.Equals(value))
                {
                _testBefore = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _loopMaximum;
        
        [XmlAttribute("loopMaximum")]
        public string LoopMaximum
        {
            get
            {
                return _loopMaximum;
            }
            set
            {
                if (_loopMaximum == value)
                    return;
                if (_loopMaximum == null || value == null || !_loopMaximum.Equals(value))
                {
                _loopMaximum = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tStartEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("startEvent", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class StartEvent : CatchEvent, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private bool _isInterrupting = true;
        
        [DefaultValueAttribute(true)]
        [XmlAttribute("isInterrupting")]
        public bool IsInterrupting
        {
            get
            {
                return _isInterrupting;
            }
            set
            {
                if (!_isInterrupting.Equals(value))
                {
                _isInterrupting = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSubChoreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("subChoreography", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class SubChoreography : ChoreographyActivity, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<FlowElement> _flowElement;
        
        [XmlElement("adHocSubProcess", Type=typeof(AdHocSubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("boundaryEvent", Type=typeof(BoundaryEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("businessRuleTask", Type=typeof(BusinessRuleTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("callActivity", Type=typeof(CallActivity), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("callChoreography", Type=typeof(CallChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("choreographyTask", Type=typeof(ChoreographyTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("complexGateway", Type=typeof(ComplexGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataObject", Type=typeof(DataObject), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataObjectReference", Type=typeof(DataObjectReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("dataStoreReference", Type=typeof(DataStoreReference), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("endEvent", Type=typeof(EndEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("event", Type=typeof(Event), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("eventBasedGateway", Type=typeof(EventBasedGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("exclusiveGateway", Type=typeof(ExclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("implicitThrowEvent", Type=typeof(ImplicitThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("inclusiveGateway", Type=typeof(InclusiveGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("intermediateCatchEvent", Type=typeof(IntermediateCatchEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("intermediateThrowEvent", Type=typeof(IntermediateThrowEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("manualTask", Type=typeof(ManualTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("parallelGateway", Type=typeof(ParallelGateway), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("receiveTask", Type=typeof(ReceiveTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("scriptTask", Type=typeof(ScriptTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("sendTask", Type=typeof(SendTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("sequenceFlow", Type=typeof(SequenceFlow), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("serviceTask", Type=typeof(ServiceTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("startEvent", Type=typeof(StartEvent), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("subChoreography", Type=typeof(SubChoreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("subProcess", Type=typeof(SubProcess), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("task", Type=typeof(Task), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("transaction", Type=typeof(Transaction), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("userTask", Type=typeof(UserTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("flowElement", Order=0)]
        public List<FlowElement> FlowElement
        {
            get
            {
                return _flowElement;
            }
            set
            {
                if (_flowElement == value)
                    return;
                if (_flowElement == null || value == null || !_flowElement.SequenceEqual(value))
                {
                _flowElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the FlowElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool FlowElementSpecified
        {
            get
            {
                return (this.FlowElement.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="SubChoreography" /> class.</para>
        /// </summary>
        public SubChoreography()
        {
            this._flowElement = new List<FlowElement>();
            this._artifact = new List<Artifact>();
        }
        
        [XmlIgnore]
        private List<Artifact> _artifact;
        
        [XmlElement("association", Type=typeof(Association), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("group", Type=typeof(Group), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("textAnnotation", Type=typeof(TextAnnotation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=1)]
        [XmlElement("artifact", Order=1)]
        public List<Artifact> Artifact
        {
            get
            {
                return _artifact;
            }
            set
            {
                if (_artifact == value)
                    return;
                if (_artifact == null || value == null || !_artifact.SequenceEqual(value))
                {
                _artifact = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Artifact collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ArtifactSpecified
        {
            get
            {
                return (this.Artifact.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tSubConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("subConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class SubConversation : ConversationNode, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<ConversationNode> _conversationNode;
        
        [XmlElement("callConversation", Type=typeof(CallConversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("conversation", Type=typeof(Conversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("subConversation", Type=typeof(SubConversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=0)]
        [XmlElement("conversationNode", Order=0)]
        public List<ConversationNode> ConversationNode
        {
            get
            {
                return _conversationNode;
            }
            set
            {
                if (_conversationNode == value)
                    return;
                if (_conversationNode == null || value == null || !_conversationNode.SequenceEqual(value))
                {
                _conversationNode = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ConversationNode collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ConversationNodeSpecified
        {
            get
            {
                return (this.ConversationNode.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="SubConversation" /> class.</para>
        /// </summary>
        public SubConversation()
        {
            this._conversationNode = new List<ConversationNode>();
        }
    }
    
    
    [Serializable]
    [XmlType("tTerminateEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("terminateEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class TerminateEventDefinition : EventDefinition, INotifyPropertyChanged
    {
    }
    
    
    [Serializable]
    [XmlType("tTextAnnotation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("textAnnotation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class TextAnnotation : Artifact, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private TText _text;
        
        [XmlElement("text", Order=0)]
        public TText Text
        {
            get
            {
                return _text;
            }
            set
            {
                if (_text == value)
                    return;
                if (_text == null || value == null || !_text.Equals(value))
                {
                _text = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _textFormat = "text/plain";
        
        [DefaultValueAttribute("text/plain")]
        [XmlAttribute("textFormat")]
        public string TextFormat
        {
            get
            {
                return _textFormat;
            }
            set
            {
                if (_textFormat == value)
                    return;
                if (_textFormat == null || value == null || !_textFormat.Equals(value))
                {
                _textFormat = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tText", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("text", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class TText : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private XmlElement _any;
        
        [XmlAnyElementAttribute(Order=0)]
        public XmlElement Any
        {
            get
            {
                return _any;
            }
            set
            {
                if (_any == value)
                    return;
                if (_any == null || value == null || !_any.Equals(value))
                {
                _any = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlTextAttribute()]
        public string[] Text { get; set; }
    }
    
    
    [Serializable]
    [XmlType("tTimerEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("timerEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class TimerEventDefinition : EventDefinition, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private Expression _timeDate;
        
        [XmlElement("timeDate", Order=0)]
        public Expression TimeDate
        {
            get
            {
                return _timeDate;
            }
            set
            {
                if (_timeDate == value)
                    return;
                if (_timeDate == null || value == null || !_timeDate.Equals(value))
                {
                _timeDate = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private Expression _timeDuration;
        
        [XmlElement("timeDuration", Order=1)]
        public Expression TimeDuration
        {
            get
            {
                return _timeDuration;
            }
            set
            {
                if (_timeDuration == value)
                    return;
                if (_timeDuration == null || value == null || !_timeDuration.Equals(value))
                {
                _timeDuration = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private Expression _timeCycle;
        
        [XmlElement("timeCycle", Order=2)]
        public Expression TimeCycle
        {
            get
            {
                return _timeCycle;
            }
            set
            {
                if (_timeCycle == value)
                    return;
                if (_timeCycle == null || value == null || !_timeCycle.Equals(value))
                {
                _timeCycle = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tTransaction", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("transaction", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Transaction : SubProcess, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private string _method = "##Compensate";
        
        [DefaultValueAttribute("##Compensate")]
        [XmlAttribute("method")]
        public string Method
        {
            get
            {
                return _method;
            }
            set
            {
                if (_method == value)
                    return;
                if (_method == null || value == null || !_method.Equals(value))
                {
                _method = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tUserTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("userTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class UserTask : Task, INotifyPropertyChanged
    {
        
        [XmlIgnore]
        private List<Rendering> _rendering;
        
        [XmlElement("rendering", Order=0)]
        public List<Rendering> Rendering
        {
            get
            {
                return _rendering;
            }
            set
            {
                if (_rendering == value)
                    return;
                if (_rendering == null || value == null || !_rendering.SequenceEqual(value))
                {
                _rendering = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Rendering collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool RenderingSpecified
        {
            get
            {
                return (this.Rendering.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="UserTask" /> class.</para>
        /// </summary>
        public UserTask()
        {
            this._rendering = new List<Rendering>();
        }
        
        [XmlIgnore]
        private string _implementation = "##unspecified";
        
        [DefaultValueAttribute("##unspecified")]
        [XmlAttribute("implementation")]
        public string Implementation
        {
            get
            {
                return _implementation;
            }
            set
            {
                if (_implementation == value)
                    return;
                if (_implementation == null || value == null || !_implementation.Equals(value))
                {
                _implementation = value;
                    OnPropertyChanged();
                }
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tDefinitions", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("definitions", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Definitions : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private List<Import> _import;
        
        [XmlElement("import", Order=0)]
        public List<Import> Import
        {
            get
            {
                return _import;
            }
            set
            {
                if (_import == value)
                    return;
                if (_import == null || value == null || !_import.SequenceEqual(value))
                {
                _import = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Import collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ImportSpecified
        {
            get
            {
                return (this.Import.Count != 0);
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Initializes a new instance of the <see cref="Definitions" /> class.</para>
        /// </summary>
        public Definitions()
        {
            this._import = new List<Import>();
            this._extension = new List<Extension>();
            this._rootElement = new List<RootElement>();
            this._bpmnDiagram = new List<BpmnDiagram>();
            this._relationship = new List<Relationship>();
            this._anyAttribute = new List<XmlAttribute>();
        }
        
        [XmlIgnore]
        private List<Extension> _extension;
        
        [XmlElement("extension", Order=1)]
        public List<Extension> Extension
        {
            get
            {
                return _extension;
            }
            set
            {
                if (_extension == value)
                    return;
                if (_extension == null || value == null || !_extension.SequenceEqual(value))
                {
                _extension = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Extension collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ExtensionSpecified
        {
            get
            {
                return (this.Extension.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<RootElement> _rootElement;
        
        [XmlElement("category", Type=typeof(Category), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("collaboration", Type=typeof(Collaboration), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("choreography", Type=typeof(Choreography), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalChoreographyTask", Type=typeof(GlobalChoreographyTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalConversation", Type=typeof(GlobalConversation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("correlationProperty", Type=typeof(CorrelationProperty), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("dataStore", Type=typeof(DataStore), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("endPoint", Type=typeof(EndPoint), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("error", Type=typeof(Error), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("escalation", Type=typeof(Escalation), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("eventDefinition", Type=typeof(EventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("cancelEventDefinition", Type=typeof(CancelEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("compensateEventDefinition", Type=typeof(CompensateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("conditionalEventDefinition", Type=typeof(ConditionalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("errorEventDefinition", Type=typeof(ErrorEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("escalationEventDefinition", Type=typeof(EscalationEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("linkEventDefinition", Type=typeof(LinkEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("messageEventDefinition", Type=typeof(MessageEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("signalEventDefinition", Type=typeof(SignalEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("terminateEventDefinition", Type=typeof(TerminateEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("timerEventDefinition", Type=typeof(TimerEventDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalBusinessRuleTask", Type=typeof(GlobalBusinessRuleTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalManualTask", Type=typeof(GlobalManualTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalScriptTask", Type=typeof(GlobalScriptTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalTask", Type=typeof(GlobalTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("globalUserTask", Type=typeof(GlobalUserTask), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("interface", Type=typeof(Interface), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("itemDefinition", Type=typeof(ItemDefinition), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("message", Type=typeof(Message), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("partnerEntity", Type=typeof(PartnerEntity), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("partnerRole", Type=typeof(PartnerRole), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("process", Type=typeof(Process), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("resource", Type=typeof(Resource), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("signal", Type=typeof(Signal), Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", Order=2)]
        [XmlElement("rootElement", Order=2)]
        public List<RootElement> RootElement
        {
            get
            {
                return _rootElement;
            }
            set
            {
                if (_rootElement == value)
                    return;
                if (_rootElement == null || value == null || !_rootElement.SequenceEqual(value))
                {
                _rootElement = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the RootElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool RootElementSpecified
        {
            get
            {
                return (this.RootElement.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<BpmnDiagram> _bpmnDiagram;
        
        [XmlElement("BPMNDiagram", Order=3, Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
        public List<BpmnDiagram> BpmnDiagram
        {
            get
            {
                return _bpmnDiagram;
            }
            set
            {
                if (_bpmnDiagram == value)
                    return;
                if (_bpmnDiagram == null || value == null || !_bpmnDiagram.SequenceEqual(value))
                {
                _bpmnDiagram = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the BpmnDiagram collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool BpmnDiagramSpecified
        {
            get
            {
                return (this.BpmnDiagram.Count != 0);
            }
        }
        
        [XmlIgnore]
        private List<Relationship> _relationship;
        
        [XmlElement("relationship", Order=4)]
        public List<Relationship> Relationship
        {
            get
            {
                return _relationship;
            }
            set
            {
                if (_relationship == value)
                    return;
                if (_relationship == null || value == null || !_relationship.SequenceEqual(value))
                {
                _relationship = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Relationship collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool RelationshipSpecified
        {
            get
            {
                return (this.Relationship.Count != 0);
            }
        }
        
        [XmlIgnore]
        private string _id;
        
        [XmlAttribute("id")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (_id == value)
                    return;
                if (_id == null || value == null || !_id.Equals(value))
                {
                _id = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _name;
        
        [XmlAttribute("name")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name == value)
                    return;
                if (_name == null || value == null || !_name.Equals(value))
                {
                _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _targetNamespace;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("targetNamespace")]
        public string TargetNamespace
        {
            get
            {
                return _targetNamespace;
            }
            set
            {
                if (_targetNamespace == value)
                    return;
                if (_targetNamespace == null || value == null || !_targetNamespace.Equals(value))
                {
                _targetNamespace = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _expressionLanguage = "http://www.w3.org/1999/XPath";
        
        [DefaultValueAttribute("http://www.w3.org/1999/XPath")]
        [XmlAttribute("expressionLanguage")]
        public string ExpressionLanguage
        {
            get
            {
                return _expressionLanguage;
            }
            set
            {
                if (_expressionLanguage == value)
                    return;
                if (_expressionLanguage == null || value == null || !_expressionLanguage.Equals(value))
                {
                _expressionLanguage = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _typeLanguage = "http://www.w3.org/2001/XMLSchema";
        
        [DefaultValueAttribute("http://www.w3.org/2001/XMLSchema")]
        [XmlAttribute("typeLanguage")]
        public string TypeLanguage
        {
            get
            {
                return _typeLanguage;
            }
            set
            {
                if (_typeLanguage == value)
                    return;
                if (_typeLanguage == null || value == null || !_typeLanguage.Equals(value))
                {
                _typeLanguage = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _exporter;
        
        [XmlAttribute("exporter")]
        public string Exporter
        {
            get
            {
                return _exporter;
            }
            set
            {
                if (_exporter == value)
                    return;
                if (_exporter == null || value == null || !_exporter.Equals(value))
                {
                _exporter = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _exporterVersion;
        
        [XmlAttribute("exporterVersion")]
        public string ExporterVersion
        {
            get
            {
                return _exporterVersion;
            }
            set
            {
                if (_exporterVersion == value)
                    return;
                if (_exporterVersion == null || value == null || !_exporterVersion.Equals(value))
                {
                _exporterVersion = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private List<XmlAttribute> _anyAttribute;

        [XmlAnyAttributeAttribute]
        public List<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            set
            {
                if (_anyAttribute == value)
                    return;
                if (_anyAttribute == null || value == null || !_anyAttribute.SequenceEqual(value))
                {
                _anyAttribute = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AnyAttribute collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AnyAttributeSpecified
        {
            get
            {
                return (this.AnyAttribute.Count != 0);
            }
        }
    }
    
    
    [Serializable]
    [XmlType("tImport", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    [DebuggerStepThrough()]
    [DesignerCategory("code")]
    [XmlRoot("import", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
    public partial class Import : INotifyPropertyChanged
    {
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [XmlIgnore]
        private string _namespace;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("namespace")]
        public string Namespace
        {
            get
            {
                return _namespace;
            }
            set
            {
                if (_namespace == value)
                    return;
                if (_namespace == null || value == null || !_namespace.Equals(value))
                {
                _namespace = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _location;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("location")]
        public string Location
        {
            get
            {
                return _location;
            }
            set
            {
                if (_location == value)
                    return;
                if (_location == null || value == null || !_location.Equals(value))
                {
                _location = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [XmlIgnore]
        private string _importType;
        
        [Required(AllowEmptyStrings=true)]
        [XmlAttribute("importType")]
        public string ImportType
        {
            get
            {
                return _importType;
            }
            set
            {
                if (_importType == value)
                    return;
                if (_importType == null || value == null || !_importType.Equals(value))
                {
                _importType = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
