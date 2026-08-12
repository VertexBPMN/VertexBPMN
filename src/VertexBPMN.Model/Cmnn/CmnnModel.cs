using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace VertexBPMN.Domain.Model.Cmn;


public record CmmnModel(
    string Id,
    string Name,
    List<CmmnPlanItem> PlanItems,
    List<CmmnSentry> Sentries,
    List<CmmnCaseFileItem> CaseFileItems,
    Dictionary<string, string> Attributes = null
);
    /// <summary>
    /// <para>tExtensionElements is a container for extension elements from
    ///        other namespaces.</para>
    /// </summary>
    [Description("tExtensionElements is a container for extension elements from other namespaces.")]    
    [Serializable]
    [XmlType("tExtensionElements", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("extensionElements", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnExtensionElements
    {
        public CmmnExtensionElements()
        {
            Any = new Collection<XmlElement>();
        }

        [XmlAnyElement(Order = 0)]
        public Collection<XmlElement> Any { get; set; } = new Collection<XmlElement>();

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
    }


    [Serializable]
    [XmlType("tRelationship", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("relationship", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnRelationship : CmmnElement
    {
        public CmmnRelationship()
        {
            Source = new Collection<XmlQualifiedName>();
            Target = new Collection<XmlQualifiedName>();
            Type = string.Empty;
            DirectionValue = CmmnRelationshipDirection.None;
            DirectionValueSpecified = false;
        }

        [XmlIgnore]
        private Collection<XmlQualifiedName> _source;

        [Required(AllowEmptyStrings = true)]
        [XmlElement("source", Order = 0)]
        public Collection<XmlQualifiedName> Source
        {
            get
            {
                return _source;
            }
            private set
            {
                _source = value;
            }
        }

        [XmlIgnore]
        private Collection<XmlQualifiedName> _target;

        [Required(AllowEmptyStrings = true)]
        [XmlElement("target", Order = 1)]
        public Collection<XmlQualifiedName> Target
        {
            get
            {
                return _target;
            }
            private set
            {
                _target = value;
            }
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("type")]
        public string Type { get; set; } = string.Empty;


        [XmlAttribute("direction")]
        public CmmnRelationshipDirection DirectionValue { get; set; } = CmmnRelationshipDirection.None;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the Direction property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool DirectionValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<CmmnRelationshipDirection> Direction
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
                this.DirectionValue = value.GetValueOrDefault();
                this.DirectionValueSpecified = value.HasValue;
            }
        }
    }

    /// <summary>
    /// <para>tCmmnElement is the base type for ALL CMMN complex types except tExpression.</para>
    /// </summary>
    [Description("tCmmnElement is the base type for ALL CMMN complex types except tExpression.")]

    [Serializable]
    [XmlType("tCmmnElement", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnApplicabilityRule))]
    [XmlInclude(typeof(CmmnArtifact))]
    [XmlInclude(typeof(CmmnAssociation))]
    [XmlInclude(typeof(CmmnCase))]
    [XmlInclude(typeof(CmmnCaseFile))]
    [XmlInclude(typeof(CmmnCaseFileItem))]
    [XmlInclude(typeof(CmmnCaseFileItemDefinition))]
    [XmlInclude(typeof(CmmnCaseFileItemOnPart))]
    [XmlInclude(typeof(CmmnCaseFileItemStartTrigger))]
    [XmlInclude(typeof(CmmnCaseParameter))]
    [XmlInclude(typeof(CmmnCaseRoles))]
    [XmlInclude(typeof(CmmnCaseTask))]
    [XmlInclude(typeof(CmmnChildren))]
    [XmlInclude(typeof(CmmnCriterion))]
    [XmlInclude(typeof(CmmnDecision))]
    [XmlInclude(typeof(CmmnDecisionParameter))]
    [XmlInclude(typeof(CmmnDecisionTask))]
    [XmlInclude(typeof(CmmnDiscretionaryItem))]
    [XmlInclude(typeof(CmmnEntryCriterion))]
    [XmlInclude(typeof(CmmnEventListener))]
    [XmlInclude(typeof(CmmnExitCriterion))]
    [XmlInclude(typeof(CmmnHumanTask))]
    [XmlInclude(typeof(CmmnIfPart))]
    [XmlInclude(typeof(CmmnManualActivationRule))]
    [XmlInclude(typeof(CmmnMilestone))]
    [XmlInclude(typeof(CmmnOnPart))]
    [XmlInclude(typeof(CmmnParameter))]
    [XmlInclude(typeof(CmmnParameterMapping))]
    [XmlInclude(typeof(CmmnPlanFragment))]
    [XmlInclude(typeof(CmmnPlanItem))]
    [XmlInclude(typeof(CmmnPlanItemControl))]
    [XmlInclude(typeof(CmmnPlanItemOnPart))]
    [XmlInclude(typeof(CmmnPlanItemStartTrigger))]
    [XmlInclude(typeof(CmmnPlanningTable))]
    [XmlInclude(typeof(CmmnProcess))]
    [XmlInclude(typeof(CmmnProcessParameter))]
    [XmlInclude(typeof(CmmnProcessTask))]
    [XmlInclude(typeof(CmmnProperty))]
    [XmlInclude(typeof(CmmnRelationship))]
    [XmlInclude(typeof(CmmnRepetitionRule))]
    [XmlInclude(typeof(CmmnRequiredRule))]
    [XmlInclude(typeof(CmmnRole))]
    [XmlInclude(typeof(CmmnSentry))]
    [XmlInclude(typeof(CmmnStage))]
    [XmlInclude(typeof(CmmnStartTrigger))]
    [XmlInclude(typeof(CmmnTableItem))]
    [XmlInclude(typeof(CmmnTask))]
    [XmlInclude(typeof(CmmnTextAnnotation))]
    [XmlInclude(typeof(CmmnTimerEventListener))]
    [XmlInclude(typeof(CmmnUserEventListener))]
    public record CmmnElement
    {
        public CmmnElement()
        {
            Documentation = new Collection<CmmnDocumentation>();
            ExtensionElements = new CmmnExtensionElements();
            Id = string.Empty;
            AnyAttribute = new Collection<XmlAttribute>();
        }

        [XmlIgnore]
        private Collection<CmmnDocumentation> _documentation;

        [XmlElement("documentation", Order = 0)]
        public Collection<CmmnDocumentation> Documentation
        {
            get
            {
                return _documentation;
            }
            private set
            {
                _documentation = value;
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

        [XmlElement("extensionElements", Order = 1)]
        public CmmnExtensionElements ExtensionElements { get; set; } = new CmmnExtensionElements();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore]
        private Collection<XmlAttribute> _anyAttribute;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            private set
            {
                _anyAttribute = value;
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
    [XmlType("tDocumentation", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("documentation", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDocumentation
    {
        public CmmnDocumentation()
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

        [XmlIgnore]
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

        [XmlText]
        public string[] Text { get; set; } = new string[0];
    }


    [Serializable]
    [XmlType("tRelationshipDirection", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public enum CmmnRelationshipDirection
    {

        None,

        Forward,

        Backward,

        Both,
    }


    [Serializable]
    [XmlType("tArtifact", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("artifact", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnAssociation))]
    [XmlInclude(typeof(CmmnTextAnnotation))]
    public record CmmnArtifact : CmmnElement
    {
        public CmmnArtifact() : base() { }
    }


    [Serializable]
    [XmlType("tAssociation", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("association", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnAssociation : CmmnArtifact
    {
        public CmmnAssociation() : base()
        {
            SourceRef = string.Empty;
            TargetRef = string.Empty;
            AssociationDirectionValue = CmmnAssociationDirection.None;
            AssociationDirectionValueSpecified = false;
        }

        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;

        [XmlAttribute("targetRef")]
        public string TargetRef { get; set; } = string.Empty;


        [XmlAttribute("associationDirection")]
        public CmmnAssociationDirection AssociationDirectionValue { get; set; } = CmmnAssociationDirection.None;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the AssociationDirection property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool AssociationDirectionValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<CmmnAssociationDirection> AssociationDirection
        {
            get
            {
                if (this.AssociationDirectionValueSpecified)
                {
                    return this.AssociationDirectionValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.AssociationDirectionValue = value.GetValueOrDefault();
                this.AssociationDirectionValueSpecified = value.HasValue;
            }
        }
    }


    [Serializable]
    [XmlType("tAssociationDirection", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public enum CmmnAssociationDirection
    {

        None,

        One,

        Both,
    }


    [Serializable]
    [XmlType("tTextAnnotation", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("textAnnotation", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnTextAnnotation : CmmnArtifact
    {
        public CmmnTextAnnotation() : base()
        {
            Text = string.Empty;
            TextFormat = string.Empty;
        }

        [XmlElement("text", Order = 0)]
        public string Text { get; set; } = string.Empty;

        [XmlAttribute("textFormat")]
        public string TextFormat { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tCmmnElementWithMixedContent is the base type for complex type tExpression 
    ///        and tDocumentation. It is identical to tCmmnElement except that it allows 
    ///        for mixed content.</para>
    /// </summary>
    [Description(("tCmmnElementWithMixedContent is the base type for complex type tExpression and tD" +
        "ocumentation. It is identical to tCmmnElement except that it allows for mixed co" +
        "ntent."))]

    [Serializable]
    [XmlType("tCmmnElementWithMixedContent", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnExpression))]
    public record CmmnElementWithMixedContent
    {
        public CmmnElementWithMixedContent()
        {
            Documentation = new Collection<CmmnDocumentation>();
            ExtensionElements = new CmmnExtensionElements();
            Id = string.Empty;
            AnyAttribute = new Collection<XmlAttribute>();
            Text = new string[0];
        }

        [XmlIgnore]
        private Collection<CmmnDocumentation> _documentation;

        [XmlElement("documentation", Order = 0)]
        public Collection<CmmnDocumentation> Documentation
        {
            get
            {
                return _documentation;
            }
            private set
            {
                _documentation = value;
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

        [XmlElement("extensionElements", Order = 1)]
        public CmmnExtensionElements ExtensionElements { get; set; } = new CmmnExtensionElements();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore]
        private Collection<XmlAttribute> _anyAttribute;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            private set
            {
                _anyAttribute = value;
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

        [XmlText]
        public string[] Text { get; set; } = new string[0];
    }

    /// <summary>
    /// <para>tCase defines the type of element "case".</para>
    /// <para>case is the root element for all CMMN case models. It is the container
    ///        for the Case File and Plan Model.</para>
    /// </summary>
    [Description(("tCase defines the type of element \"case\". case is the root element for all CMMN ca" +
        "se models. It is the container for the Case File and Plan Model."))]

    [Serializable]
    [XmlType("tCase", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("case", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCase : CmmnElement
    {
        public CmmnCase() : base()
        {
            CaseFileModel = new CmmnCaseFile();
            CasePlanModel = new CmmnStage();
            CaseRoles = new CmmnCaseRoles();
            Input = new Collection<CmmnCaseParameter>();
            Output = new Collection<CmmnCaseParameter>();
            Name = string.Empty;
        }

        [XmlElement("caseFileModel", Order = 0)]
        public CmmnCaseFile CaseFileModel { get; set; } = new CmmnCaseFile();

        [XmlElement("casePlanModel", Order = 1)]
        public CmmnStage CasePlanModel { get; set; } = new CmmnStage();

        [XmlElement("caseRoles", Order = 2)]
        public CmmnCaseRoles CaseRoles { get; set; } = new CmmnCaseRoles();

        [XmlIgnore]
        private Collection<CmmnCaseParameter> _input;

        [XmlElement("input", Order = 3)]
        public Collection<CmmnCaseParameter> Input
        {
            get
            {
                return _input;
            }
            private set
            {
                _input = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Input collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InputSpecified
        {
            get
            {
                return (this.Input.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnCaseParameter> _output;

        [XmlElement("output", Order = 4)]
        public Collection<CmmnCaseParameter> Output
        {
            get
            {
                return _output;
            }
            private set
            {
                _output = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Output collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutputSpecified
        {
            get
            {
                return (this.Output.Count != 0);
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tCaseFile defines the type of element "caseFile".</para>
    /// <para>caseFile is the root element for the CMMN Case File Model 
    ///        and is a container for CaseFileItems.</para>
    /// </summary>
    [Description(("tCaseFile defines the type of element \"caseFile\". caseFile is the root element fo" +
        "r the CMMN Case File Model and is a container for CaseFileItems."))]

    [Serializable]
    [XmlType("tCaseFile", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseFile", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseFile : CmmnElement
    {
        public CmmnCaseFile() : base()
        {
            CaseFileItem = new Collection<CmmnCaseFileItem>();
        }

        [XmlIgnore]
        private Collection<CmmnCaseFileItem> _caseFileItem;

        [XmlElement("caseFileItem", Order = 0)]
        public Collection<CmmnCaseFileItem> CaseFileItem
        {
            get
            {
                return _caseFileItem;
            }
            private set
            {
                _caseFileItem = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CaseFileItem collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CaseFileItemSpecified
        {
            get
            {
                return (this.CaseFileItem.Count != 0);
            }
        }
    }

    /// <summary>
    /// <para>tCaseFileItem defines the type of element "caseFileItem".</para>
    /// <para>caseFileItem is the root element for CMMN data.</para>
    /// </summary>
    [Description(("tCaseFileItem defines the type of element \"caseFileItem\". caseFileItem is the roo" +
        "t element for CMMN data."))]

    [Serializable]
    [XmlType("tCaseFileItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseFileItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseFileItem : CmmnElement
    {
        public CmmnCaseFileItem() : base()
        {
            Children = new CmmnChildren();
            Name = string.Empty;
            Multiplicity = MultiplicityEnum.Unspecified;
            DefinitionRef = new XmlQualifiedName();
            SourceRef = string.Empty;
            TargetRefs = new Collection<string>();
        }

        [XmlElement("children", Order = 0)]
        public CmmnChildren Children { get; set; } = new CmmnChildren();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlIgnore]
        private MultiplicityEnum _multiplicity = MultiplicityEnum.Unspecified;

        [DefaultValue(MultiplicityEnum.Unspecified)]
        [XmlAttribute("multiplicity")]
        public MultiplicityEnum Multiplicity
        {
            get
            {
                return _multiplicity;
            }
            set
            {
                _multiplicity = value;
            }
        }

        /// <summary>
        /// <para>definitinRef MUST refer to a "caseFileItemDefinition" element. Since
        ///              CaseFileItemDefinition is re-usable, QName is used for reference.</para>
        /// </summary>
        [Description(("definitinRef MUST refer to a \"caseFileItemDefinition\" element. Since CaseFileItem" +
            "Definition is re-usable, QName is used for reference."))]
        [XmlAttribute("definitionRef")]
        public XmlQualifiedName DefinitionRef { get; set; } = new XmlQualifiedName();

        /// <summary>
        /// <para>sourceRef MUST refer to a "caseFileItem" element in the case where this
        ///              "caseFileItem" has a parent.</para>
        /// </summary>
        [Description(("sourceRef MUST refer to a \"caseFileItem\" element in the case where this \"caseFil" +
            "eItem\" has a parent."))]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;

        [XmlIgnore]
        private Collection<string> _targetRefs;

        /// <summary>
        /// <para>If this "caseFileItem" maintains references to "caseFileItem" childs, then 
        ///              targetRefs MUST refer to "caseFileItem" elements; the targets of this caseFileItem.</para>
        /// </summary>
        [Description(("If this \"caseFileItem\" maintains references to \"caseFileItem\" childs, then target" +
            "Refs MUST refer to \"caseFileItem\" elements; the targets of this caseFileItem."))]
        [XmlAttribute("targetRefs")]
        public Collection<string> TargetRefs
        {
            get
            {
                return _targetRefs;
            }
            private set
            {
                _targetRefs = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the TargetRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool TargetRefsSpecified
        {
            get
            {
                return (this.TargetRefs.Count != 0);
            }
        }
    }

    /// <summary>
    /// <para>tChildren defines a container for zero or more "caseFileItem" elements.</para>
    /// </summary>
    [Description("tChildren defines a container for zero or more \"caseFileItem\" elements.")]

    [Serializable]
    [XmlType("tChildren", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    public record CmmnChildren : CmmnElement
    {
        public CmmnChildren() : base()
        {
            CaseFileItem = new Collection<CmmnCaseFileItem>();
        }

        [XmlIgnore]
        private Collection<CmmnCaseFileItem> _caseFileItem;

        [XmlElement("caseFileItem", Order = 0)]
        public Collection<CmmnCaseFileItem> CaseFileItem
        {
            get
            {
                return _caseFileItem;
            }
            private set
            {
                _caseFileItem = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CaseFileItem collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CaseFileItemSpecified
        {
            get
            {
                return (this.CaseFileItem.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("MultiplicityEnum", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public enum MultiplicityEnum
    {

        ZeroOrOne,

        ZeroOrMore,

        ExactlyOne,

        OneOrMore,

        Unspecified,

        Unknown,
    }

    /// <summary>
    /// <para>tStage defines the type for element "stage"</para>
    /// <para>stage represents a Stage in the Case Model and comprises of
    ///        zero or one PlanningTable, zero or more PlanItemDefinition elements
    ///        and if the Stage is the outermost Stage, zero or more references to
    ///        exitCriterion.</para>
    /// </summary>
    [Description(("tStage defines the type for element \"stage\" stage represents a Stage in the Case " +
        "Model and comprises of zero or one PlanningTable, zero or more PlanItemDefinitio" +
        "n elements and if the Stage is the outermost Stage, zero or more references to e" +
        "xitCriterion."))]

    [Serializable]
    [XmlType("tStage", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("stage", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnStage : CmmnPlanFragment
    {
        public CmmnStage() : base()
        {
            PlanningTable = new CmmnPlanningTable();
            PlanItemDefinition = new Collection<CmmnPlanItemDefinition>();
            ExitCriterion = new Collection<CmmnExitCriterion>();
            AutoComplete = false;
        }

        [XmlElement("planningTable", Order = 0)]
        public CmmnPlanningTable PlanningTable { get; set; } = new CmmnPlanningTable();

        [XmlIgnore]
        private Collection<CmmnPlanItemDefinition> _planItemDefinition;

        [XmlElement("task", Type = typeof(CmmnTask), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("humanTask", Type = typeof(CmmnHumanTask), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("processTask", Type = typeof(CmmnProcessTask), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("caseTask", Type = typeof(CmmnCaseTask), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("decisionTask", Type = typeof(CmmnDecisionTask), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("eventListener", Type = typeof(CmmnEventListener), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("userEventListener", Type = typeof(CmmnUserEventListener), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("timerEventListener", Type = typeof(CmmnTimerEventListener), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("milestone", Type = typeof(CmmnMilestone), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("planFragment", Type = typeof(CmmnPlanFragment), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("stage", Type = typeof(CmmnStage), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("planItemDefinition", Order = 1)]
        public Collection<CmmnPlanItemDefinition> PlanItemDefinition
        {
            get
            {
                return _planItemDefinition;
            }
            private set
            {
                _planItemDefinition = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the PlanItemDefinition collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool PlanItemDefinitionSpecified
        {
            get
            {
                return (this.PlanItemDefinition.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnExitCriterion> _exitCriterion;

        [XmlElement("exitCriterion", Order = 2)]
        public Collection<CmmnExitCriterion> ExitCriterion
        {
            get
            {
                return _exitCriterion;
            }
            private set
            {
                _exitCriterion = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ExitCriterion collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ExitCriterionSpecified
        {
            get
            {
                return (this.ExitCriterion.Count != 0);
            }
        }

        [XmlIgnore]
        private bool _autoComplete = false;

        [DefaultValue(false)]
        [XmlAttribute("autoComplete")]
        public bool AutoComplete
        {
            get
            {
                return _autoComplete;
            }
            set
            {
                _autoComplete = value;
            }
        }
    }

    /// <summary>
    /// <para>tPlanFragment defines the type for element "planFragment"</para>
    /// <para>planFragment is the root element for PlanItems that should go into
    ///        the plan as a unit.</para>
    /// </summary>
    [Description(("tPlanFragment defines the type for element \"planFragment\" planFragment is the roo" +
        "t element for PlanItems that should go into the plan as a unit."))]

    [Serializable]
    [XmlType("tPlanFragment", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planFragment", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnStage))]
    public record CmmnPlanFragment : CmmnPlanItemDefinition
    {
        public CmmnPlanFragment() : base()
        {
            PlanItem = new Collection<CmmnPlanItem>();
            Sentry = new Collection<CmmnSentry>();
        }

        [XmlIgnore]
        private Collection<CmmnPlanItem> _planItem;

        [XmlElement("planItem", Order = 0)]
        public Collection<CmmnPlanItem> PlanItem
        {
            get
            {
                return _planItem;
            }
            private set
            {
                _planItem = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the PlanItem collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool PlanItemSpecified
        {
            get
            {
                return (this.PlanItem.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnSentry> _sentry;

        [XmlElement("sentry", Order = 1)]
        public Collection<CmmnSentry> Sentry
        {
            get
            {
                return _sentry;
            }
            private set
            {
                _sentry = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Sentry collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool SentrySpecified
        {
            get
            {
                return (this.Sentry.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("tPlanItemDefinition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planItemDefinition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnCaseTask))]
    [XmlInclude(typeof(CmmnDecisionTask))]
    [XmlInclude(typeof(CmmnEventListener))]
    [XmlInclude(typeof(CmmnHumanTask))]
    [XmlInclude(typeof(CmmnMilestone))]
    [XmlInclude(typeof(CmmnPlanFragment))]
    [XmlInclude(typeof(CmmnProcessTask))]
    [XmlInclude(typeof(CmmnStage))]
    [XmlInclude(typeof(CmmnTask))]
    [XmlInclude(typeof(CmmnTimerEventListener))]
    [XmlInclude(typeof(CmmnUserEventListener))]
    public record CmmnPlanItemDefinition : CmmnElement
    {
        public CmmnPlanItemDefinition() : base()
        {
            DefaultControl = new CmmnPlanItemControl();
        }

        [XmlElement("defaultControl", Order = 0)]
        public CmmnPlanItemControl DefaultControl { get; set; } = new CmmnPlanItemControl();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tPlanItemcontrol defines the type of element "planItemControl".</para>
    /// <para>planItemControl is the root element for Case Plan Control elements
    ///        including the RepetitionRule, RequiredRule and ManualActivationRule.</para>
    /// </summary>
    [Description(("tPlanItemcontrol defines the type of element \"planItemControl\". planItemControl i" +
        "s the root element for Case Plan Control elements including the RepetitionRule, " +
        "RequiredRule and ManualActivationRule."))]

    [Serializable]
    [XmlType("tPlanItemControl", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planItemControl", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnPlanItemControl : CmmnElement
    {
        public CmmnPlanItemControl() : base()
        {
            RepetitionRule = new CmmnRepetitionRule();
            RequiredRule = new CmmnRequiredRule();
            ManualActivationRule = new CmmnManualActivationRule();
        }

        [XmlElement("repetitionRule", Order = 0)]
        public CmmnRepetitionRule RepetitionRule { get; set; } = new CmmnRepetitionRule();

        [XmlElement("requiredRule", Order = 1)]
        public CmmnRequiredRule RequiredRule { get; set; } = new CmmnRequiredRule();

        [XmlElement("manualActivationRule", Order = 2)]
        public CmmnManualActivationRule ManualActivationRule { get; set; } = new CmmnManualActivationRule();
    }

    /// <summary>
    /// <para>tRepetitionRule defines the type of element "repetitionRule".</para>
    /// <para>repetitionRule is the root element for specifying a 
    ///        repetition rule for a PlanItemDefinition element.</para>
    /// </summary>
    [Description(("tRepetitionRule defines the type of element \"repetitionRule\". repetitionRule is t" +
        "he root element for specifying a repetition rule for a PlanItemDefinition elemen" +
        "t."))]

    [Serializable]
    [XmlType("tRepetitionRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("repetitionRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnRepetitionRule : CmmnElement
    {
        public CmmnRepetitionRule() : base()
        {
            Condition = new CmmnExpression();
            Name = string.Empty;
            ContextRef = string.Empty;
        }

        [XmlElement("condition", Order = 0)]
        public CmmnExpression Condition { get; set; } = new CmmnExpression();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>contextRef MUST refer a CaseFileItem if specified.</para>
        /// </summary>
        [Description("contextRef MUST refer a CaseFileItem if specified.")]
        [XmlAttribute("contextRef")]
        public string ContextRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tExpression", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("expression", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnExpression : CmmnElementWithMixedContent
    {
        public CmmnExpression() : base()
        {
            Language = string.Empty;
        }

        [XmlAttribute("language")]
        public string Language { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tRequiredRule defines the type of element "requiredRule".</para>
    /// <para>requiredRule is the root element for specifying a 
    ///        required rule for a PlanItemDefinition element.</para>
    /// </summary>
    [Description(("tRequiredRule defines the type of element \"requiredRule\". requiredRule is the roo" +
        "t element for specifying a required rule for a PlanItemDefinition element."))]

    [Serializable]
    [XmlType("tRequiredRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("requiredRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnRequiredRule : CmmnElement
    {
        public CmmnRequiredRule() : base()
        {
            Condition = new CmmnExpression();
            Name = string.Empty;
            ContextRef = string.Empty;
        }

        [XmlElement("condition", Order = 0)]
        public CmmnExpression Condition { get; set; } = new CmmnExpression();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>contextRef MUST refer a CaseFileItem if specified.</para>
        /// </summary>
        [Description("contextRef MUST refer a CaseFileItem if specified.")]
        [XmlAttribute("contextRef")]
        public string ContextRef { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tManualActivationRule defines the type of element "manualActivationRule".</para>
    /// <para>manualActivationRule is the root element for specifying an 
    ///        manual activation rule for a PlanItemDefinition element.</para>
    /// </summary>
    [Description(("tManualActivationRule defines the type of element \"manualActivationRule\". manualA" +
        "ctivationRule is the root element for specifying an manual activation rule for a" +
        " PlanItemDefinition element."))]

    [Serializable]
    [XmlType("tManualActivationRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("manualActivationRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnManualActivationRule : CmmnElement
    {
        public CmmnManualActivationRule() : base()
        {
            Condition = new CmmnExpression();
            Name = string.Empty;
            ContextRef = string.Empty;
        }

        [XmlElement("condition", Order = 0)]
        public CmmnExpression Condition { get; set; } = new CmmnExpression();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>contextRef MUST refer a CaseFileItem if specified.</para>
        /// </summary>
        [Description("contextRef MUST refer a CaseFileItem if specified.")]
        [XmlAttribute("contextRef")]
        public string ContextRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tPlanItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnPlanItem : CmmnElement
    {
        public CmmnPlanItem() : base()
        {
            ItemControl = new CmmnPlanItemControl();
            EntryCriterion = new Collection<CmmnEntryCriterion>();
            ExitCriterion = new Collection<CmmnExitCriterion>();
            Name = string.Empty;
            DefinitionRef = string.Empty;
        }

        [XmlElement("itemControl", Order = 0)]
        public CmmnPlanItemControl ItemControl { get; set; } = new CmmnPlanItemControl();

        [XmlIgnore]
        private Collection<CmmnEntryCriterion> _entryCriterion;

        [XmlElement("entryCriterion", Order = 1)]
        public Collection<CmmnEntryCriterion> EntryCriterion
        {
            get
            {
                return _entryCriterion;
            }
            private set
            {
                _entryCriterion = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EntryCriterion collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EntryCriterionSpecified
        {
            get
            {
                return (this.EntryCriterion.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnExitCriterion> _exitCriterion;

        [XmlElement("exitCriterion", Order = 2)]
        public Collection<CmmnExitCriterion> ExitCriterion
        {
            get
            {
                return _exitCriterion;
            }
            private set
            {
                _exitCriterion = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ExitCriterion collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ExitCriterionSpecified
        {
            get
            {
                return (this.ExitCriterion.Count != 0);
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>definitionRef refers a "planItemDefinition" element.</para>
        /// </summary>
        [Description("definitionRef refers a \"planItemDefinition\" element.")]
        [XmlAttribute("definitionRef")]
        public string DefinitionRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tEntryCriterion", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("entryCriterion", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnEntryCriterion : CmmnCriterion
    {
        public CmmnEntryCriterion() : base() { }
    }


    [Serializable]
    [XmlType("tCriterion", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnEntryCriterion))]
    [XmlInclude(typeof(CmmnExitCriterion))]
    public record CmmnCriterion : CmmnElement
    {
        public CmmnCriterion() : base()
        {
            Name = string.Empty;
            SentryRef = string.Empty;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>sentryRef refers to an existing Sentry in this casePlanModel</para>
        /// </summary>
        [Description("sentryRef refers to an existing Sentry in this casePlanModel")]
        [XmlAttribute("sentryRef")]
        public string SentryRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tExitCriterion", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("exitCriterion", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnExitCriterion : CmmnCriterion
    {
        public CmmnExitCriterion() : base() { }
    }

    /// <summary>
    /// <para>tSentry defines the type of element "sentry"</para>
    /// <para>sentry is the root element of "Sentry" in the Case Model and
    ///        comprises of zero or more OnParts and zero or one IfPart.</para>
    /// </summary>
    [Description(("tSentry defines the type of element \"sentry\" sentry is the root element of \"Sentr" +
        "y\" in the Case Model and comprises of zero or more OnParts and zero or one IfPar" +
        "t."))]

    [Serializable]
    [XmlType("tSentry", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("sentry", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnSentry : CmmnElement
    {
        public CmmnSentry() : base()
        {
            OnPart = new Collection<CmmnOnPart>();
            IfPart = new CmmnIfPart();
            Name = string.Empty;
        }

        [XmlIgnore]
        private Collection<CmmnOnPart> _onPart;

        [XmlElement("caseFileItemOnPart", Type = typeof(CmmnCaseFileItemOnPart), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 0)]
        [XmlElement("planItemOnPart", Type = typeof(CmmnPlanItemOnPart), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 0)]
        [XmlElement("onPart", Order = 0)]
        public Collection<CmmnOnPart> OnPart
        {
            get
            {
                return _onPart;
            }
            private set
            {
                _onPart = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the OnPart collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OnPartSpecified
        {
            get
            {
                return (this.OnPart.Count != 0);
            }
        }

        [XmlElement("ifPart", Order = 1)]
        public CmmnIfPart IfPart { get; set; } = new CmmnIfPart();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tOnPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("onPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnCaseFileItemOnPart))]
    [XmlInclude(typeof(CmmnPlanItemOnPart))]
    public record CmmnOnPart : CmmnElement
    {
        public CmmnOnPart() : base()
        {
            Name = string.Empty;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tIfPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("ifPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnIfPart : CmmnElement
    {
        public CmmnIfPart() : base()
        {
            Condition = new CmmnExpression();
        }

        [XmlElement("condition", Order = 0)]
        public CmmnExpression Condition { get; set; } = new CmmnExpression();
    }


    [Serializable]
    [XmlType("tCaseFileItemOnPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseFileItemOnPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseFileItemOnPart : CmmnOnPart
    {
        public CmmnCaseFileItemOnPart() : base()
        {
            StandardEventValue = CaseFileItemTransition.Create;
            StandardEventValueSpecified = false;
            SourceRef = string.Empty;
        }

        [XmlElement("standardEvent", Order = 0)]
        public CaseFileItemTransition StandardEventValue { get; set; } = CaseFileItemTransition.Create;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the StandardEvent property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool StandardEventValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<CaseFileItemTransition> StandardEvent
        {
            get
            {
                if (this.StandardEventValueSpecified)
                {
                    return this.StandardEventValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.StandardEventValue = value.GetValueOrDefault();
                this.StandardEventValueSpecified = value.HasValue;
            }
        }

        /// <summary>
        /// <para>sourceRef refers a "caseFileItem" element</para>
        /// </summary>
        [Description("sourceRef refers a \"caseFileItem\" element")]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>Enumeration of CaseFileItem transitions.</para>
    /// </summary>
    [Description("Enumeration of CaseFileItem transitions.")]

    [Serializable]
    [XmlType("CaseFileItemTransition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public enum CaseFileItemTransition
    {

        [XmlEnum("addChild")]
        AddChild,

        [XmlEnum("addReference")]
        AddReference,

        [XmlEnum("create")]
        Create,

        [XmlEnum("delete")]
        Delete,

        [XmlEnum("removeChild")]
        RemoveChild,

        [XmlEnum("removeReference")]
        RemoveReference,

        [XmlEnum("replace")]
        Replace,

        [XmlEnum("update")]
        Update,
    }


    [Serializable]
    [XmlType("tPlanItemOnPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planItemOnPart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnPlanItemOnPart : CmmnOnPart
    {
        public CmmnPlanItemOnPart() : base()
        {
            StandardEventValue = PlanItemTransition.Create;
            StandardEventValueSpecified = false;
            SourceRef = string.Empty;
            ExitCriterionRef = string.Empty;
        }

        [XmlElement("standardEvent", Order = 0)]
        public PlanItemTransition StandardEventValue { get; set; } = PlanItemTransition.Create;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the StandardEvent property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool StandardEventValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<PlanItemTransition> StandardEvent
        {
            get
            {
                if (this.StandardEventValueSpecified)
                {
                    return this.StandardEventValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.StandardEventValue = value.GetValueOrDefault();
                this.StandardEventValueSpecified = value.HasValue;
            }
        }

        /// <summary>
        /// <para>sourceRef refers a "planItem" element</para>
        /// </summary>
        [Description("sourceRef refers a \"planItem\" element")]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;

        /// <summary>
        /// <para>exitCriterionRef refers a "ExitCriterion" element that is contained in the
        ///              "planItem" referred by sourceRef</para>
        /// </summary>
        [Description(("exitCriterionRef refers a \"ExitCriterion\" element that is contained in the \"planI" +
            "tem\" referred by sourceRef"))]
        [XmlAttribute("exitCriterionRef")]
        public string ExitCriterionRef { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>Enumeration of PlanItem transitions.</para>
    /// </summary>
    [Description("Enumeration of PlanItem transitions.")]

    [Serializable]
    [XmlType("PlanItemTransition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public enum PlanItemTransition
    {

        [XmlEnum("close")]
        Close,

        [XmlEnum("complete")]
        Complete,

        [XmlEnum("create")]
        Create,

        [XmlEnum("disable")]
        Disable,

        [XmlEnum("enable")]
        Enable,

        [XmlEnum("exit")]
        Exit,

        [XmlEnum("fault")]
        Fault,

        [XmlEnum("manualStart")]
        ManualStart,

        [XmlEnum("occur")]
        Occur,

        [XmlEnum("parentResume")]
        ParentResume,

        [XmlEnum("parentSuspend")]
        ParentSuspend,

        [XmlEnum("reactivate")]
        Reactivate,

        [XmlEnum("reenable")]
        Reenable,

        [XmlEnum("resume")]
        Resume,

        [XmlEnum("start")]
        Start,

        [XmlEnum("suspend")]
        Suspend,

        [XmlEnum("terminate")]
        Terminate,
    }


    [Serializable]
    [XmlType("tDiscretionaryItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("discretionaryItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDiscretionaryItem : CmmnTableItem
    {
        public CmmnDiscretionaryItem() : base()
        {
            ItemControl = new CmmnPlanItemControl();
            EntryCriterion = new Collection<CmmnEntryCriterion>();
            ExitCriterion = new Collection<CmmnExitCriterion>();
            Name = string.Empty;
            DefinitionRef = string.Empty;
        }

        [XmlElement("itemControl", Order = 0)]
        public CmmnPlanItemControl ItemControl { get; set; } = new CmmnPlanItemControl();

        [XmlIgnore]
        private Collection<CmmnEntryCriterion> _entryCriterion;

        [XmlElement("entryCriterion", Order = 1)]
        public Collection<CmmnEntryCriterion> EntryCriterion
        {
            get
            {
                return _entryCriterion;
            }
            private set
            {
                _entryCriterion = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the EntryCriterion collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool EntryCriterionSpecified
        {
            get
            {
                return (this.EntryCriterion.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnExitCriterion> _exitCriterion;

        [XmlElement("exitCriterion", Order = 2)]
        public Collection<CmmnExitCriterion> ExitCriterion
        {
            get
            {
                return _exitCriterion;
            }
            private set
            {
                _exitCriterion = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ExitCriterion collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ExitCriterionSpecified
        {
            get
            {
                return (this.ExitCriterion.Count != 0);
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>definitionRef refers a "planItemDefinition" element</para>
        /// </summary>
        [Description("definitionRef refers a \"planItemDefinition\" element")]
        [XmlAttribute("definitionRef")]
        public string DefinitionRef { get; set; } = string.Empty;
    }



    [Serializable]
    [XmlType("tPlanningTable", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planningTable", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnPlanningTable : CmmnTableItem
    {
        public CmmnPlanningTable() : base()
        {
            TableItem = new Collection<CmmnTableItem>();
            ApplicabilityRule = new Collection<CmmnApplicabilityRule>();
        }

        [XmlIgnore]
        private Collection<CmmnTableItem> _tableItem;

        [XmlElement("planningTable", Type = typeof(CmmnPlanningTable), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 0)]
        [XmlElement("discretionaryItem", Type = typeof(CmmnDiscretionaryItem), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 0)]
        [XmlElement("tableItem", Order = 0)]
        public Collection<CmmnTableItem> TableItem
        {
            get
            {
                return _tableItem;
            }
            private set
            {
                _tableItem = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the TableItem collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool TableItemSpecified
        {
            get
            {
                return (this.TableItem.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnApplicabilityRule> _applicabilityRule;

        [XmlElement("applicabilityRule", Order = 1)]
        public Collection<CmmnApplicabilityRule> ApplicabilityRule
        {
            get
            {
                return _applicabilityRule;
            }
            private set
            {
                _applicabilityRule = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ApplicabilityRule collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ApplicabilityRuleSpecified
        {
            get
            {
                return (this.ApplicabilityRule.Count != 0);
            }
        }
    }

    /// <summary>
    /// <para>tCaseRoles defines the type of element "caseRoles inside tCase".</para>
    /// </summary>
    [Description("tCaseRoles defines the type of element \"caseRoles inside tCase\".")]

    [Serializable]
    [XmlType("tCaseRoles", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    public record CmmnCaseRoles : CmmnElement
    {
        public CmmnCaseRoles() : base()
        {
            Roles = new Collection<CmmnRole>();
        }

        [XmlElement("role", Order = 0)]
        public Collection<CmmnRole> Roles { get; set; } = new Collection<CmmnRole>();

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Role collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool RoleSpecified
        {
            get
            {
                return (this.Roles.Count() != 0);
            }
        }
    }

    /// <summary>
    /// <para>tRole defines the type of element "role".</para>
    /// <para>role is the root element for Case Roles.</para>
    /// </summary>
    [Description(("tRole defines the type of element \"role\". role is the root element for Case Roles" +
        "."))]

    [Serializable]
    [XmlType("tRole", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("role", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnRole : CmmnElement
    {
        public CmmnRole() : base()
        {
            Name = string.Empty;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tCaseParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseParameter : CmmnParameter
    {
        public CmmnCaseParameter() : base()
        {
            BindingRefinement = new CmmnExpression();
            BindingRef = string.Empty;
        }

        [XmlElement("bindingRefinement", Order = 0)]
        public CmmnExpression BindingRefinement { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>bindingRef refers a "caseFileItem" element</para>
        /// </summary>
        [Description("bindingRef refers a \"caseFileItem\" element")]
        [XmlAttribute("bindingRef")]
        public string BindingRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("parameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnCaseParameter))]
    [XmlInclude(typeof(CmmnDecisionParameter))]
    [XmlInclude(typeof(CmmnProcessParameter))]
    public record CmmnParameter : CmmnElement
    {
        public CmmnParameter() : base()
        {
            Name = string.Empty;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tCaseFileItemDefinition defines the type of element "caseFileItemDefinition"</para>
    /// <para>caseFileItemDefinition defines the type of a "caseFileItem".</para>
    /// </summary>
    [Description(("tCaseFileItemDefinition defines the type of element \"caseFileItemDefinition\" case" +
        "FileItemDefinition defines the type of a \"caseFileItem\"."))]

    [Serializable]
    [XmlType("tCaseFileItemDefinition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseFileItemDefinition", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseFileItemDefinition : CmmnElement
    {
        public CmmnCaseFileItemDefinition() : base()
        {
            Property = new Collection<CmmnProperty>();
            Name = string.Empty;
            DefinitionType = "http://www.omg.org/spec/CMMN/DefinitionType/Unspecified";
            StructureRef = new XmlQualifiedName();
            ImportRef = new XmlQualifiedName();
        }

        [XmlIgnore]
        private Collection<CmmnProperty> _property;

        [XmlElement("property", Order = 0)]
        public Collection<CmmnProperty> Property
        {
            get
            {
                return _property;
            }
            private set
            {
                _property = value;
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

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlIgnore]
        private string _definitionType = "http://www.omg.org/spec/CMMN/DefinitionType/Unspecified";

        [DefaultValue("http://www.omg.org/spec/CMMN/DefinitionType/Unspecified")]
        [XmlAttribute("definitionType")]
        public string DefinitionType
        {
            get
            {
                return _definitionType;
            }
            set
            {
                _definitionType = value;
            }
        }

        /// <summary>
        /// <para>structureRef refers a structure, for example an XML-Schema element
        ///              in a XSD referred by importRef</para>
        /// </summary>
        [Description(("structureRef refers a structure, for example an XML-Schema element in a XSD refer" +
            "red by importRef"))]
        [XmlAttribute("structureRef")]
        public XmlQualifiedName StructureRef { get; set; } = new XmlQualifiedName();

        /// <summary>
        /// <para>importRef refers an "import" element under "definitions"</para>
        /// </summary>
        [Description("importRef refers an \"import\" element under \"definitions\"")]
        [XmlAttribute("importRef")]
        public XmlQualifiedName ImportRef { get; set; } = new XmlQualifiedName();
    }


    [Serializable]
    [XmlType("tProperty", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("property", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnProperty : CmmnElement
    {
        public CmmnProperty() : base()
        {
            Name = string.Empty;
            Type = "http://www.omg.org/spec/CMMN/PropertyType/Unspecified";
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlIgnore]
        private string _type = "http://www.omg.org/spec/CMMN/PropertyType/Unspecified";

        [DefaultValue("http://www.omg.org/spec/CMMN/PropertyType/Unspecified")]
        [XmlAttribute("type")]
        public string Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
            }
        }
    }

    /// <summary>
    /// <para>task represents an (abstract) Task in the Case Model and comprises
    ///        of input, output and a flag if the task is blocking or not.</para>
    /// </summary>
    [Description(("task represents an (abstract) Task in the Case Model and comprises of input, outp" +
        "ut and a flag if the task is blocking or not."))]

    [Serializable]
    [XmlType("tTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("task", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnCaseTask))]
    [XmlInclude(typeof(CmmnDecisionTask))]
    [XmlInclude(typeof(CmmnHumanTask))]
    [XmlInclude(typeof(CmmnProcessTask))]
    public record CmmnTask : CmmnPlanItemDefinition
    {
        public CmmnTask() : base()
        {
            Input = new Collection<CmmnCaseParameter>();
            Output = new Collection<CmmnCaseParameter>();
            IsBlocking = true;
        }

        [XmlIgnore]
        private Collection<CmmnCaseParameter> _input;

        [XmlElement("input", Order = 0)]
        public Collection<CmmnCaseParameter> Input
        {
            get
            {
                return _input;
            }
            private set
            {
                _input = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Input collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InputSpecified
        {
            get
            {
                return (this.Input.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnCaseParameter> _output;

        [XmlElement("output", Order = 1)]
        public Collection<CmmnCaseParameter> Output
        {
            get
            {
                return _output;
            }
            private set
            {
                _output = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Output collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutputSpecified
        {
            get
            {
                return (this.Output.Count != 0);
            }
        }

        [XmlIgnore]
        private bool _isBlocking = true;

        [DefaultValue(true)]
        [XmlAttribute("isBlocking")]
        public bool IsBlocking
        {
            get
            {
                return _isBlocking;
            }
            set
            {
                _isBlocking = value;
            }
        }
    }

    /// <summary>
    /// <para>event represents an (abstract) Event in the Case Model.</para>
    /// </summary>
    [Description("event represents an (abstract) Event in the Case Model.")]

    [Serializable]
    [XmlType("tEventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("eventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnTimerEventListener))]
    [XmlInclude(typeof(CmmnUserEventListener))]
    public record CmmnEventListener : CmmnPlanItemDefinition
    {
        public CmmnEventListener() : base() { }
    }


    [Serializable]
    [XmlType("tMilestone", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("milestone", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnMilestone : CmmnPlanItemDefinition
    {
        public CmmnMilestone() : base() { }
    }


    [Serializable]
    [XmlType("tProcessParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("processParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnProcessParameter : CmmnParameter
    {
        public CmmnProcessParameter() : base() { }
    }


    [Serializable]
    [XmlType("tDecisionParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("decisionParameter", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDecisionParameter : CmmnParameter
    {
        public CmmnDecisionParameter() : base() { }
    }

    /// <summary>
    /// <para>tHumanTask defines the type of element "humanTask"</para>
    /// <para>humanTask represents a HumanTask in the Case Model and comprises of
    ///        zero or one PlanningTable and a reference to a Role (the performer of
    ///        the human task).</para>
    /// </summary>
    [Description(("tHumanTask defines the type of element \"humanTask\" humanTask represents a HumanTa" +
        "sk in the Case Model and comprises of zero or one PlanningTable and a reference " +
        "to a Role (the performer of the human task)."))]

    [Serializable]
    [XmlType("tHumanTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("humanTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnHumanTask : CmmnTask
    {
        public CmmnHumanTask() : base()
        {
            PlanningTable = new CmmnPlanningTable();
            PerformerRef = string.Empty;
        }

        [XmlElement("planningTable", Order = 0)]
        public CmmnPlanningTable PlanningTable { get; set; } = new CmmnPlanningTable();

        /// <summary>
        /// <para>performerRef refers a "role" element</para>
        /// </summary>
        [Description("performerRef refers a \"role\" element")]
        [XmlAttribute("performerRef")]
        public string PerformerRef { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tProcessTask defines the type of element "process"</para>
    /// <para>processTask represents a ProcessTask in the Case Model and comprises of
    ///        ParameterMappings and a reference to an (abstract) Process.</para>
    /// </summary>
    [Description(("tProcessTask defines the type of element \"process\" processTask represents a Proce" +
        "ssTask in the Case Model and comprises of ParameterMappings and a reference to a" +
        "n (abstract) Process."))]

    [Serializable]
    [XmlType("tProcessTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("processTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnProcessTask : CmmnTask
    {
        public CmmnProcessTask() : base()
        {
            ParameterMapping = new Collection<CmmnParameterMapping>();
            ProcessRefExpression = new CmmnExpression();
            ProcessRef = new XmlQualifiedName();
        }

        [XmlIgnore]
        private Collection<CmmnParameterMapping> _parameterMapping;

        [XmlElement("parameterMapping", Order = 0)]
        public Collection<CmmnParameterMapping> ParameterMapping
        {
            get
            {
                return _parameterMapping;
            }
            private set
            {
                _parameterMapping = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParameterMapping collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParameterMappingSpecified
        {
            get
            {
                return (this.ParameterMapping.Count != 0);
            }
        }

        /// <summary>
        /// <para>processRefExpression is mutually exclusive to processRef. It allows the selection of a
        ///                process at runtime via an expression rather then at design time with processRef.</para>
        /// </summary>
        [Description(("processRefExpression is mutually exclusive to processRef. It allows the selection" +
            " of a process at runtime via an expression rather then at design time with proce" +
            "ssRef."))]
        [XmlElement("processRefExpression", Order = 1)]
        public CmmnExpression ProcessRefExpression { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>processRef refers a "process" element which is re-usable and can
        ///              be imported via some other file. processRef is mutually exclusive to "processRefExpression"</para>
        /// </summary>
        [Description(("processRef refers a \"process\" element which is re-usable and can be imported via " +
            "some other file. processRef is mutually exclusive to \"processRefExpression\""))]
        [XmlAttribute("processRef")]
        public XmlQualifiedName ProcessRef { get; set; } = new XmlQualifiedName();
    }


    [Serializable]
    [XmlType("tParameterMapping", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("parameterMapping", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnParameterMapping : CmmnElement
    {
        public CmmnParameterMapping() : base()
        {
            Transformation = new CmmnExpression();
            SourceRef = string.Empty;
            TargetRef = string.Empty;
        }

        [XmlElement("transformation", Order = 0)]
        public CmmnExpression Transformation { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>sourceRef refers a "parameter" element</para>
        /// </summary>
        [Description("sourceRef refers a \"parameter\" element")]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;

        /// <summary>
        /// <para>targetRef refers a "parameter" element</para>
        /// </summary>
        [Description("targetRef refers a \"parameter\" element")]
        [XmlAttribute("targetRef")]
        public string TargetRef { get; set; } = string.Empty;
    }

    /// <summary>
    /// <para>tProcess defines the type of element "process"</para>
    /// <para>process represents an (abstract) Business Process in CMMN. It has
    ///        an implementationType, input and output and can be referred from
    ///        a ProcessTask.</para>
    /// </summary>
    [Description(("tProcess defines the type of element \"process\" process represents an (abstract) B" +
        "usiness Process in CMMN. It has an implementationType, input and output and can " +
        "be referred from a ProcessTask."))]

    [Serializable]
    [XmlType("tProcess", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("process", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnProcess : CmmnElement
    {
        public CmmnProcess() : base()
        {
            Input = new Collection<CmmnProcessParameter>();
            Output = new Collection<CmmnProcessParameter>();
            ImplementationType = "http://www.omg.org/spec/CMMN/ProcessType/Unspecified";
        }

        [XmlIgnore]
        private Collection<CmmnProcessParameter> _input;

        [XmlElement("input", Order = 0)]
        public Collection<CmmnProcessParameter> Input
        {
            get
            {
                return _input;
            }
            private set
            {
                _input = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Input collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InputSpecified
        {
            get
            {
                return (this.Input.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnProcessParameter> _output;

        [XmlElement("output", Order = 1)]
        public Collection<CmmnProcessParameter> Output
        {
            get
            {
                return _output;
            }
            private set
            {
                _output = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Output collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutputSpecified
        {
            get
            {
                return (this.Output.Count != 0);
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("externalRef")]
        public XmlQualifiedName ExternalRef { get; set; } = new XmlQualifiedName();

        [XmlIgnore]
        private string _implementationType = "http://www.omg.org/spec/CMMN/ProcessType/Unspecified";

        [DefaultValue("http://www.omg.org/spec/CMMN/ProcessType/Unspecified")]
        [XmlAttribute("implementationType")]
        public string ImplementationType
        {
            get
            {
                return _implementationType;
            }
            set
            {
                _implementationType = value;
            }
        }
    }

    /// <summary>
    /// <para>tCaseTask defines the type of element "caseTask"</para>
    /// <para>caseTask is the root element for CaseTask in the Case Model and
    ///        comprises of ParameterMappings and a reference to a Case</para>
    /// </summary>
    [Description(("tCaseTask defines the type of element \"caseTask\" caseTask is the root element for" +
        " CaseTask in the Case Model and comprises of ParameterMappings and a reference t" +
        "o a Case"))]

    [Serializable]
    [XmlType("tCaseTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseTask : CmmnTask
    {
        public CmmnCaseTask() : base()
        {
            ParameterMapping = new Collection<CmmnParameterMapping>();
            CaseRefExpression = new CmmnExpression();
            CaseRef = new XmlQualifiedName();
        }

        [XmlIgnore]
        private Collection<CmmnParameterMapping> _parameterMapping;

        [XmlElement("parameterMapping", Order = 0)]
        public Collection<CmmnParameterMapping> ParameterMapping
        {
            get
            {
                return _parameterMapping;
            }
            private set
            {
                _parameterMapping = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParameterMapping collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParameterMappingSpecified
        {
            get
            {
                return (this.ParameterMapping.Count != 0);
            }
        }

        /// <summary>
        /// <para>caseRefExpression is mutualy exclusive to caseRef and can be used to select a case 
        ///                at runtime rather then specifying caseRef at design-time.</para>
        /// </summary>
        [Description(("caseRefExpression is mutualy exclusive to caseRef and can be used to select a cas" +
            "e at runtime rather then specifying caseRef at design-time."))]
        [XmlElement("caseRefExpression", Order = 1)]
        public CmmnExpression CaseRefExpression { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>caseRef refers a "case" element which is re-usable and can 
        ///              be imported via some other file. caseRef is mutually exclusive to "caseRefExpression"</para>
        /// </summary>
        [Description(("caseRef refers a \"case\" element which is re-usable and can be imported via some o" +
            "ther file. caseRef is mutually exclusive to \"caseRefExpression\""))]
        [XmlAttribute("caseRef")]
        public XmlQualifiedName CaseRef { get; set; } = new XmlQualifiedName();
    }

    /// <summary>
    /// <para>tDecisionTask defines the type of element "decision"</para>
    /// <para>decisionTask represents a DecisionTask in the Case Model and comprises of
    ///        ParameterMappings and a reference to an (abstract) Decision.</para>
    /// </summary>
    [Description(("tDecisionTask defines the type of element \"decision\" decisionTask represents a De" +
        "cisionTask in the Case Model and comprises of ParameterMappings and a reference " +
        "to an (abstract) Decision."))]

    [Serializable]
    [XmlType("tDecisionTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("decisionTask", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDecisionTask : CmmnTask
    {
        public CmmnDecisionTask() : base()
        {
            ParameterMapping = new Collection<CmmnParameterMapping>();
            DecisionRefExpression = new CmmnExpression();
            DecisionRef = new XmlQualifiedName();
        }

        [XmlIgnore]
        private Collection<CmmnParameterMapping> _parameterMapping;

        [XmlElement("parameterMapping", Order = 0)]
        public Collection<CmmnParameterMapping> ParameterMapping
        {
            get
            {
                return _parameterMapping;
            }
            private set
            {
                _parameterMapping = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ParameterMapping collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ParameterMappingSpecified
        {
            get
            {
                return (this.ParameterMapping.Count != 0);
            }
        }

        /// <summary>
        /// <para>decisionRefExpression is mutually exclusive to decisionRef. It allows the selection of a
        ///                decision at runtime via an expression rather then at design time with decisionRef.</para>
        /// </summary>
        [Description(("decisionRefExpression is mutually exclusive to decisionRef. It allows the selecti" +
            "on of a decision at runtime via an expression rather then at design time with de" +
            "cisionRef."))]
        [XmlElement("decisionRefExpression", Order = 1)]
        public CmmnExpression DecisionRefExpression { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>decisionRef refers a "decision" element which is re-usable and can
        ///              be imported via some other file. decisionRef is mutually exclusive to decisionRefExpression</para>
        /// </summary>
        [Description(("decisionRef refers a \"decision\" element which is re-usable and can be imported vi" +
            "a some other file. decisionRef is mutually exclusive to decisionRefExpression"))]
        [XmlAttribute("decisionRef")]
        public XmlQualifiedName DecisionRef { get; set; } = new XmlQualifiedName();
    }

    /// <summary>
    /// <para>tDecision defines the type of element "decision"</para>
    /// <para>decision represents an (abstract) Decision in CMMN. It has
    ///        an implementationType, input and output and can be referred from
    ///        a DecisionTask.</para>
    /// </summary>
    [Description(("tDecision defines the type of element \"decision\" decision represents an (abstract" +
        ") Decision in CMMN. It has an implementationType, input and output and can be re" +
        "ferred from a DecisionTask."))]

    [Serializable]
    [XmlType("tDecision", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("decision", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDecision : CmmnElement
    {
        public CmmnDecision() : base()
        {
            Input = new Collection<CmmnDecisionParameter>();
            Output = new Collection<CmmnDecisionParameter>();
            Name = string.Empty;
            ImplementationType = "http://www.omg.org/spec/CMMN/DecisionType/Unspecified";
        }

        [XmlIgnore]
        private Collection<CmmnDecisionParameter> _input;

        [XmlElement("input", Order = 0)]
        public Collection<CmmnDecisionParameter> Input
        {
            get
            {
                return _input;
            }
            private set
            {
                _input = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Input collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool InputSpecified
        {
            get
            {
                return (this.Input.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnDecisionParameter> _output;

        [XmlElement("output", Order = 1)]
        public Collection<CmmnDecisionParameter> Output
        {
            get
            {
                return _output;
            }
            private set
            {
                _output = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Output collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool OutputSpecified
        {
            get
            {
                return (this.Output.Count != 0);
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("externalRef")]
        public XmlQualifiedName ExternalRef { get; set; } = new XmlQualifiedName();

        [XmlIgnore]
        private string _implementationType = "http://www.omg.org/spec/CMMN/DecisionType/Unspecified";

        [DefaultValue("http://www.omg.org/spec/CMMN/DecisionType/Unspecified")]
        [XmlAttribute("implementationType")]
        public string ImplementationType
        {
            get
            {
                return _implementationType;
            }
            set
            {
                _implementationType = value;
            }
        }
    }


    [Serializable]
    [XmlType("tUserEventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("userEventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnUserEventListener : CmmnEventListener
    {
        public CmmnUserEventListener() : base()
        {
            AuthorizedRoleRefs = new Collection<string>();
        }

        [XmlIgnore]
        private Collection<string> _authorizedRoleRefs;

        /// <summary>
        /// <para>authorizedRoleRefs refers zero or more "role" elements.</para>
        /// </summary>
        [Description("authorizedRoleRefs refers zero or more \"role\" elements.")]
        [XmlAttribute("authorizedRoleRefs")]
        public Collection<string> AuthorizedRoleRefs
        {
            get
            {
                return _authorizedRoleRefs;
            }
            private set
            {
                _authorizedRoleRefs = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AuthorizedRoleRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AuthorizedRoleRefsSpecified
        {
            get
            {
                return (this.AuthorizedRoleRefs.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("tTimerEventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("timerEventListener", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnTimerEventListener : CmmnEventListener
    {
        public CmmnTimerEventListener() : base()
        {
            TimerExpression = new CmmnExpression();
            TimerStart = new CmmnStartTrigger();
        }

        /// <summary>
        /// <para>timerExpression is supposed to be an ISO-8601 conformant expression</para>
        /// </summary>
        [Description("timerExpression is supposed to be an ISO-8601 conformant expression")]
        [XmlElement("timerExpression", Order = 0)]
        public CmmnExpression TimerExpression { get; set; } = new CmmnExpression();

        /// <summary>
        /// <para>timerStart can be used to trigger the timer after a PlanItem or CaseFileItem 
        ///                lifecycle state transition has occurred.</para>
        /// </summary>
        [Description(("timerStart can be used to trigger the timer after a PlanItem or CaseFileItem life" +
            "cycle state transition has occurred."))]
        [XmlElement("caseFileItemStartTrigger", Type = typeof(CmmnCaseFileItemStartTrigger), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("planItemStartTrigger", Type = typeof(CmmnPlanItemStartTrigger), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 1)]
        [XmlElement("timerStart", Order = 1)]
        public CmmnStartTrigger TimerStart { get; set; } = new CmmnStartTrigger();
    }


    [Serializable]
    [XmlType("tStartTrigger", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("timerStart", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnCaseFileItemStartTrigger))]
    [XmlInclude(typeof(CmmnPlanItemStartTrigger))]
    public record CmmnStartTrigger : CmmnElement
    {
        public CmmnStartTrigger() : base() { }
    }


    [Serializable]
    [XmlType("tCaseFileItemStartTrigger", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("caseFileItemStartTrigger", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnCaseFileItemStartTrigger : CmmnStartTrigger
    {
        public CmmnCaseFileItemStartTrigger() : base()
        {
            StandardEventValue = CaseFileItemTransition.Create;
            StandardEventValueSpecified = false;
            SourceRef = string.Empty;
        }

        [XmlElement("standardEvent", Order = 0)]
        public CaseFileItemTransition StandardEventValue { get; set; } = CaseFileItemTransition.Create;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the StandardEvent property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool StandardEventValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<CaseFileItemTransition> StandardEvent
        {
            get
            {
                if (this.StandardEventValueSpecified)
                {
                    return this.StandardEventValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.StandardEventValue = value.GetValueOrDefault();
                this.StandardEventValueSpecified = value.HasValue;
            }
        }

        /// <summary>
        /// <para>sourceRef refers a "caseFileItem" element</para>
        /// </summary>
        [Description("sourceRef refers a \"caseFileItem\" element")]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;
    }

    
    [Serializable]
    [XmlType("tPlanItemStartTrigger", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("planItemStartTrigger", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnPlanItemStartTrigger : CmmnStartTrigger
    {
        public CmmnPlanItemStartTrigger() : base()
        {
            StandardEventValue = PlanItemTransition.Create;
            StandardEventValueSpecified = false;
            SourceRef = string.Empty;
        }

        [XmlElement("standardEvent", Order = 0)]
        public PlanItemTransition StandardEventValue { get; set; } = PlanItemTransition.Create;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the StandardEvent property is specified.</para>
        /// </summary>
        [XmlIgnore]
        public bool StandardEventValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<PlanItemTransition> StandardEvent
        {
            get
            {
                if (this.StandardEventValueSpecified)
                {
                    return this.StandardEventValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.StandardEventValue = value.GetValueOrDefault();
                this.StandardEventValueSpecified = value.HasValue;
            }
        }

        /// <summary>
        /// <para>sourceRef refers a "planItem" element</para>
        /// </summary>
        [Description("sourceRef refers a \"planItem\" element")]
        [XmlAttribute("sourceRef")]
        public string SourceRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("tTableItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("tableItem", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [XmlInclude(typeof(CmmnDiscretionaryItem))]
    [XmlInclude(typeof(CmmnPlanningTable))]
    public record CmmnTableItem : CmmnElement
    {
        public CmmnTableItem() : base()
        {
            ApplicabilityRuleRefs = new Collection<string>();
            AuthorizedRoleRefs = new Collection<string>();
        }

        [XmlIgnore]
        private Collection<string> _applicabilityRuleRefs;

        /// <summary>
        /// <para>applicabilityRuleRefs refers one or more "applicabilityRule" elements.</para>
        /// </summary>
        [Description("applicabilityRuleRefs refers one or more \"applicabilityRule\" elements.")]
        [XmlAttribute("applicabilityRuleRefs")]
        public Collection<string> ApplicabilityRuleRefs
        {
            get
            {
                return _applicabilityRuleRefs;
            }
            private set
            {
                _applicabilityRuleRefs = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the ApplicabilityRuleRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ApplicabilityRuleRefsSpecified
        {
            get
            {
                return (this.ApplicabilityRuleRefs.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<string> _authorizedRoleRefs;

        /// <summary>
        /// <para>authorizedRoleRefs refers zero or more "role" elements.</para>
        /// </summary>
        [Description("authorizedRoleRefs refers zero or more \"role\" elements.")]
        [XmlAttribute("authorizedRoleRefs")]
        public Collection<string> AuthorizedRoleRefs
        {
            get
            {
                return _authorizedRoleRefs;
            }
            private set
            {
                _authorizedRoleRefs = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the AuthorizedRoleRefs collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool AuthorizedRoleRefsSpecified
        {
            get
            {
                return (this.AuthorizedRoleRefs.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("tApplicabilityRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("applicabilityRule", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnApplicabilityRule : CmmnElement
    {
        public CmmnApplicabilityRule() : base()
        {
            Condition = new CmmnExpression();
            Name = string.Empty;
            ContextRef = string.Empty;
        }

        [XmlElement("condition", Order = 0)]
        public CmmnExpression Condition { get; set; } = new CmmnExpression();

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// <para>contexRef refers a "caseFileItem" element</para>
        /// </summary>
        [Description("contexRef refers a \"caseFileItem\" element")]
        [XmlAttribute("contextRef")]
        public string ContextRef { get; set; } = string.Empty;
    }


    [Serializable]
    [XmlType("Color", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("Color", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    public partial class Color
    {
        public Color()
        {
            Red = 0;
            Green = 0;
            Blue = 0;
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

    [Serializable]
    [XmlType("Point", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("Point", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    public partial class Point
    {
        public Point()
        {
            X = 0.0;
            Y = 0.0;
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

    [Serializable]
    [XmlType("Dimension", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("Dimension", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    public partial class Dimension
    {
        public Dimension()
        {
            Width = 0.0;
            Height = 0.0;
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

    [Serializable]
    [XmlType("Bounds", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("Bounds", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
    public partial class Bounds
    {
        public Bounds()
        {
            X = 0.0;
            Y = 0.0;
            Width = 0.0;
            Height = 0.0;
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

    [Serializable]
    [XmlType("AlignmentKind", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
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

    [Serializable]
    [XmlType("KnownColor", Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
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

    [Serializable]
    [XmlType("DiagramElement", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNDiagramElement", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [XmlInclude(typeof(CmmnDiagram))]
    [XmlInclude(typeof(CmmnEdge))]
    [XmlInclude(typeof(CmmnLabel))]
    [XmlInclude(typeof(CmmnShape))]
    [XmlInclude(typeof(Diagram))]
    [XmlInclude(typeof(Edge))]
    [XmlInclude(typeof(Shape))]
    public abstract partial class DiagramElement
    {
        public DiagramElement()
        {
            Extension = new DiagramElementExtension();
            Style = new CmmnStyle();
            SharedStyle = string.Empty;
            Id = string.Empty;
            AnyAttribute = new Collection<XmlAttribute>();
        }

        [XmlElement("extension", Order = 0)]
        public DiagramElementExtension Extension { get; set; } = new DiagramElementExtension();

        /// <summary>
        /// <para>an optional locally-owned style for this diagram element.</para>
        /// </summary>
        [Description("an optional locally-owned style for this diagram element.")]
        [XmlElement("CMMNStyle", Type = typeof(CmmnStyle), Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI", Order = 1)]
        [XmlElement("Style", Order = 1)]
        public Style Style { get; set; } = new CmmnStyle();

        /// <summary>
        /// <para>a reference to an optional shared style element for this diagram element.</para>
        /// </summary>
        [Description("a reference to an optional shared style element for this diagram element.")]
        [XmlAttribute("sharedStyle")]
        public string SharedStyle { get; set; } = string.Empty;

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore]
        private Collection<XmlAttribute> _anyAttribute;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            private set
            {
                _anyAttribute = value;
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
    [XmlType("DiagramElementExtension", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI", AnonymousType = true)]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    public partial class DiagramElementExtension
    {
        public DiagramElementExtension()
        {
            Any = new Collection<XmlElement>();
        }

        [XmlIgnore]
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
        [XmlIgnore]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }
    }

    /// <summary>
    /// <para>Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves.</para>
    /// <para>This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence</para>
    /// </summary>
    [Description(@"Style contains formatting properties that affect the appearance or style of diagram elements, including diagram themselves. This element should never be instantiated directly, but rather concrete implementation should. It is placed there only to be referred in the sequence")]

    [Serializable]
    [XmlType("Style", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("Style", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [XmlInclude(typeof(CmmnStyle))]
    public abstract partial class Style
    {
        protected Style()
        {
            Extension = new StyleExtension();
            Id = string.Empty;
            AnyAttribute = new Collection<XmlAttribute>();
        }

        [XmlElement("extension", Order = 0)]
        public StyleExtension Extension { get; set; } = new StyleExtension();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlIgnore]
        private Collection<XmlAttribute> _anyAttribute;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            private set
            {
                _anyAttribute = value;
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
    [XmlType("StyleExtension", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI", AnonymousType = true)]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    public partial class StyleExtension
    {
        public StyleExtension()
        {
            Any = new Collection<XmlElement>();
        }

        [XmlIgnore]
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
        [XmlIgnore]
        public bool AnySpecified
        {
            get
            {
                return (this.Any.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("Edge", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnEdge))]
    public abstract partial class Edge : DiagramElement
    {
        protected Edge() : base()
        {
            Waypoint = new Collection<Point>();
        }

        [XmlIgnore]
        private Collection<Point> _waypoint;

        /// <summary>
        /// <para>an optional list of points relative to the origin of the nesting diagram that specifies the connected line segments of the edge</para>
        /// </summary>
        [Description(("an optional list of points relative to the origin of the nesting diagram that spe" +
            "cifies the connected line segments of the edge"))]
        [XmlElement("waypoint", Order = 0)]
        public Collection<Point> Waypoint
        {
            get
            {
                return _waypoint;
            }
            private set
            {
                _waypoint = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Waypoint collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool WaypointSpecified
        {
            get
            {
                return (this.Waypoint.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("Diagram", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnDiagram))]
    public abstract partial class Diagram : DiagramElement
    {
        protected Diagram() : base()
        {
            Name = string.Empty;
            Documentation = string.Empty;
            ResolutionValue = 0.0;
            ResolutionValueSpecified = false;
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
        [XmlIgnore]
        
        public bool ResolutionValueSpecified { get; set; } = false;

        /// <summary>
        /// <para>the resolution of the diagram expressed in user units per inch.</para>
        /// </summary>
        [XmlIgnore]
        public Nullable<double> Resolution
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


    [Serializable]
    [XmlType("Shape", Namespace = "http://www.omg.org/spec/CMMN/20151109/DI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlInclude(typeof(CmmnLabel))]
    [XmlInclude(typeof(CmmnShape))]
    public abstract partial class Shape : DiagramElement
    {
        protected Shape() : base()
        {
            Bounds = new Bounds();
        }

        /// <summary>
        /// <para>the optional bounds of the shape relative to the origin of its nesting plane.</para>
        /// </summary>
        [Description("the optional bounds of the shape relative to the origin of its nesting plane.")]
        [XmlElement("Bounds", Order = 0, Namespace = "http://www.omg.org/spec/CMMN/20151109/DC")]
        public Bounds Bounds { get; set; } = new Bounds();
    }


    [Serializable]
    [XmlType("CMMNDI", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNDI", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class Cmmndi
    {
        public Cmmndi()
        {
            CmmnDiagram = new Collection<CmmnDiagram>();
            CmmnStyle = new Collection<CmmnStyle>();
        }

        [XmlIgnore]
        private Collection<CmmnDiagram> _cmmnDiagram;

        [XmlElement("CMMNDiagram", Order = 0)]
        public Collection<CmmnDiagram> CmmnDiagram
        {
            get
            {
                return _cmmnDiagram;
            }
            private set
            {
                _cmmnDiagram = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CmmnDiagram collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CmmnDiagramSpecified
        {
            get
            {
                return (this.CmmnDiagram.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnStyle> _cmmnStyle;

        [XmlElement("CMMNStyle", Order = 1)]
        public Collection<CmmnStyle> CmmnStyle
        {
            get
            {
                return _cmmnStyle;
            }
            private set
            {
                _cmmnStyle = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CmmnStyle collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CmmnStyleSpecified
        {
            get
            {
                return (this.CmmnStyle.Count != 0);
            }
        }
    }


    [Serializable]
    [XmlType("CMMNDiagram", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNDiagram", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class CmmnDiagram : Diagram
    {
        public CmmnDiagram() : base()
        {
            Size = new Dimension();
            CmmnDiagramElement = new Collection<DiagramElement>();
            CmmnElementRef = new XmlQualifiedName();
        }

        [XmlElement("Size", Order = 0)]
        public Dimension Size { get; set; } = new Dimension();

        [XmlIgnore]
        private Collection<DiagramElement> _cmmnDiagramElement;

        [XmlElement("CMMNShape", Type = typeof(CmmnShape), Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI", Order = 1)]
        [XmlElement("CMMNEdge", Type = typeof(CmmnEdge), Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI", Order = 1)]
        [XmlElement("CMMNDiagramElement", Order = 1)]
        public Collection<DiagramElement> CmmnDiagramElement
        {
            get
            {
                return _cmmnDiagramElement;
            }
            private set
            {
                _cmmnDiagramElement = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CmmnDiagramElement collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CmmnDiagramElementSpecified
        {
            get
            {
                return (this.CmmnDiagramElement.Count != 0);
            }
        }

        [XmlAttribute("cmmnElementRef")]
        public XmlQualifiedName CmmnElementRef { get; set; } = new XmlQualifiedName();
    }


    [Serializable]
    [XmlType("CMMNStyle", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNStyle", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class CmmnStyle : Style
    {
        public CmmnStyle() : base()
        {
            FillColor = new Color();
            StrokeColor = new Color();
            FontColor = new Color();
            FontFamily = string.Empty;
            FontSizeValue = 0.0;
            FontSizeValueSpecified = false;
            FontItalicValue = false;
            FontItalicValueSpecified = false;
            FontBoldValue = false;
            FontBoldValueSpecified = false;
            FontUnderlineValue = false;
            FontUnderlineValueSpecified = false;
            FontStrikeThroughValue = false;
            FontStrikeThroughValueSpecified = false;
            LabelHorizontalAlignementValue = AlignmentKind.Center;
            LabelHorizontalAlignementValueSpecified = false;
            LabelVerticalAlignmentValue = AlignmentKind.Center;
            LabelVerticalAlignmentValueSpecified = false;
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
        [XmlIgnore]
        
        public bool FontSizeValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<double> FontSize
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
        [XmlIgnore]
        
        public bool FontItalicValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> FontItalic
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
        [XmlIgnore]
        
        public bool FontBoldValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> FontBold
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
        [XmlIgnore]
        
        public bool FontUnderlineValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> FontUnderline
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
        [XmlIgnore]
        
        public bool FontStrikeThroughValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> FontStrikeThrough
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
        public AlignmentKind LabelHorizontalAlignementValue { get; set; } = AlignmentKind.Center;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelHorizontalAlignement property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool LabelHorizontalAlignementValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<AlignmentKind> LabelHorizontalAlignement
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
        public AlignmentKind LabelVerticalAlignmentValue { get; set; } = AlignmentKind.Center;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the LabelVerticalAlignment property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool LabelVerticalAlignmentValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<AlignmentKind> LabelVerticalAlignment
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


    [Serializable]
    [XmlType("CMMNShape", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNShape", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class CmmnShape : Shape
    {
        public CmmnShape() : base()
        {
            CmmnLabel = new CmmnLabel();
            CmmnElementRef = new XmlQualifiedName();
            IsCollapsedValue = false;
            IsCollapsedValueSpecified = false;
            IsPlanningTableCollapsedValue = false;
            IsPlanningTableCollapsedValueSpecified = false;
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("CMMNLabel", Order = 0)]
        public CmmnLabel CmmnLabel { get; set; } = new CmmnLabel();

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("cmmnElementRef")]
        public XmlQualifiedName CmmnElementRef { get; set; } = new XmlQualifiedName();


        [XmlAttribute("isCollapsed")]
        public bool IsCollapsedValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsCollapsed property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool IsCollapsedValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> IsCollapsed
        {
            get
            {
                if (this.IsCollapsedValueSpecified)
                {
                    return this.IsCollapsedValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.IsCollapsedValue = value.GetValueOrDefault();
                this.IsCollapsedValueSpecified = value.HasValue;
            }
        }


        [XmlAttribute("isPlanningTableCollapsed")]
        public bool IsPlanningTableCollapsedValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsPlanningTableCollapsed property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool IsPlanningTableCollapsedValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> IsPlanningTableCollapsed
        {
            get
            {
                if (this.IsPlanningTableCollapsedValueSpecified)
                {
                    return this.IsPlanningTableCollapsedValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.IsPlanningTableCollapsedValue = value.GetValueOrDefault();
                this.IsPlanningTableCollapsedValueSpecified = value.HasValue;
            }
        }
    }


    [Serializable]
    [XmlType("CMMNLabel", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNLabel", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class CmmnLabel : Shape
    {
        public CmmnLabel() : base() { }
    }


    [Serializable]
    [XmlType("CMMNEdge", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("CMMNEdge", Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
    public partial class CmmnEdge : Edge
    {
        public CmmnEdge() : base()
        {
            CmmnLabel = new CmmnLabel();
            CmmnElementRef = new XmlQualifiedName();
            SourceCmmnElementRef = new XmlQualifiedName();
            TargetCmmnElementRef = new XmlQualifiedName();
            IsStandardEventVisibleValue = false;
            IsStandardEventVisibleValueSpecified = false;
        }

        [Required(AllowEmptyStrings = true)]
        [XmlElement("CMMNLabel", Order = 0)]
        public CmmnLabel CmmnLabel { get; set; } = new CmmnLabel();

        [XmlAttribute("cmmnElementRef")]
        public XmlQualifiedName CmmnElementRef { get; set; } = new XmlQualifiedName();

        [XmlAttribute("sourceCMMNElementRef")]
        public XmlQualifiedName SourceCmmnElementRef { get; set; } = new XmlQualifiedName();

        [XmlAttribute("targetCMMNElementRef")]
        public XmlQualifiedName TargetCmmnElementRef { get; set; } = new XmlQualifiedName();


        [XmlAttribute("isStandardEventVisible")]
        public bool IsStandardEventVisibleValue { get; set; } = false;

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the IsStandardEventVisible property is specified.</para>
        /// </summary>
        [XmlIgnore]
        
        public bool IsStandardEventVisibleValueSpecified { get; set; } = false;

        [XmlIgnore]
        public Nullable<bool> IsStandardEventVisible
        {
            get
            {
                if (this.IsStandardEventVisibleValueSpecified)
                {
                    return this.IsStandardEventVisibleValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.IsStandardEventVisibleValue = value.GetValueOrDefault();
                this.IsStandardEventVisibleValueSpecified = value.HasValue;
            }
        }
    }



/// <summary>
/// <para>tDefinitions defines the type of element "definitions".</para>
/// <para>definitions is the root element of ALL CMMN elements. It is used as a container
///        for CMMN elements that might be re-used.</para>
/// </summary>
[Description(("tDefinitions defines the type of element \"definitions\". definitions is the root e" +
        "lement of ALL CMMN elements. It is used as a container for CMMN elements that mi" +
        "ght be re-used."))]

    [Serializable]
    [XmlType("tDefinitions", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("definitions", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnDefinitions
    {
        public CmmnDefinitions()
        {
            Import = new Collection<CmmnImport>();
            CaseFileItemDefinition = new Collection<CmmnCaseFileItemDefinition>();
            Case = new Collection<CmmnCase>();
            Process = new Collection<CmmnProcess>();
            Decision = new Collection<CmmnDecision>();
            Relationship = new Collection<CmmnRelationship>();
            Artifact = new Collection<CmmnArtifact>();
            AnyAttribute = new Collection<XmlAttribute>();
            Name = string.Empty;
            TargetNamespace = string.Empty;
            ExpressionLanguage = "http://www.w3.org/1999/XPath";
            Exporter = string.Empty;
            ExporterVersion = string.Empty;
            Author = string.Empty;
            CreationDateValueSpecified = false;
        }

        [XmlIgnore]
        private Collection<CmmnImport> _import;

        [XmlElement("import", Order = 0)]
        public Collection<CmmnImport> Import
        {
            get
            {
                return _import;
            }
            private set
            {
                _import = value;
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

        [XmlIgnore]
        private Collection<CmmnCaseFileItemDefinition> _caseFileItemDefinition;

        [XmlElement("caseFileItemDefinition", Order = 1)]
        public Collection<CmmnCaseFileItemDefinition> CaseFileItemDefinition
        {
            get
            {
                return _caseFileItemDefinition;
            }
            private set
            {
                _caseFileItemDefinition = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the CaseFileItemDefinition collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CaseFileItemDefinitionSpecified
        {
            get
            {
                return (this.CaseFileItemDefinition.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnCase> _case;

        [XmlElement("case", Order = 2)]
        public Collection<CmmnCase> Case
        {
            get
            {
                return _case;
            }
            private set
            {
                _case = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Case collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool CaseSpecified
        {
            get
            {
                return (this.Case.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnProcess> _process;

        [XmlElement("process", Order = 3)]
        public Collection<CmmnProcess> Process
        {
            get
            {
                return _process;
            }
            private set
            {
                _process = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Process collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool ProcessSpecified
        {
            get
            {
                return (this.Process.Count != 0);
            }
        }

        [XmlIgnore]
        private Collection<CmmnDecision> _decision;

        [XmlElement("decision", Order = 4)]
        public Collection<CmmnDecision> Decision
        {
            get
            {
                return _decision;
            }
            private set
            {
                _decision = value;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Gets a value indicating whether the Decision collection is empty.</para>
        /// </summary>
        [XmlIgnore]
        public bool DecisionSpecified
        {
            get
            {
                return (this.Decision.Count != 0);
            }
        }

        [XmlElement("extensionElements", Order = 5)]
        public CmmnExtensionElements ExtensionElements { get; set; } = new CmmnExtensionElements();

        [XmlIgnore]
        private Collection<CmmnRelationship> _relationship;

        [XmlElement("relationship", Order = 6)]
        public Collection<CmmnRelationship> Relationship
        {
            get
            {
                return _relationship;
            }
            private set
            {
                _relationship = value;
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
        private Collection<CmmnArtifact> _artifact;

        [XmlElement("association", Type = typeof(CmmnAssociation), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 7)]
        [XmlElement("textAnnotation", Type = typeof(CmmnTextAnnotation), Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL", Order = 7)]
        [XmlElement("artifact", Order = 7)]
        public Collection<CmmnArtifact> Artifact
        {
            get
            {
                return _artifact;
            }
            private set
            {
                _artifact = value;
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

        [XmlElement("CMMNDI", Order = 8, Namespace = "http://www.omg.org/spec/CMMN/20151109/CMMNDI")]
        public Cmmndi Cmmndi { get; set; } = new Cmmndi();

        [XmlAttribute("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("targetNamespace")]
        public string TargetNamespace { get; set; }

        [XmlIgnore]
        private string _expressionLanguage = "http://www.w3.org/1999/XPath";

        [DefaultValue("http://www.w3.org/1999/XPath")]
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

        [XmlAttribute("exporter")]
        public string Exporter { get; set; }

        [XmlAttribute("exporterVersion")]
        public string ExporterVersion { get; set; }

        [XmlAttribute("author")]
        public string Author { get; set; }


        [XmlAttribute("creationDate", DataType = "dateTime")]
        public DateTime CreationDateValue { get; set; }

        /// <summary>
        /// <para xml:lang="en">Gets or sets a value indicating whether the CreationDate property is specified.</para>
        /// </summary>
        [XmlIgnore]

        public bool CreationDateValueSpecified { get; set; }

        [XmlIgnore]
        public Nullable<DateTime> CreationDate
        {
            get
            {
                if (this.CreationDateValueSpecified)
                {
                    return this.CreationDateValue;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                this.CreationDateValue = value.GetValueOrDefault();
                this.CreationDateValueSpecified = value.HasValue;
            }
        }

        [XmlIgnore]
        private Collection<XmlAttribute> _anyAttribute;

        [XmlAnyAttribute]
        public Collection<XmlAttribute> AnyAttribute
        {
            get
            {
                return _anyAttribute;
            }
            private set
            {
                _anyAttribute = value;
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
    [XmlType("tImport", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlRoot("import", Namespace = "http://www.omg.org/spec/CMMN/20151109/MODEL")]
    public record CmmnImport
    {
        public CmmnImport()
        {
            Location = string.Empty;
            Namespace = string.Empty; 
            ImportType = string.Empty;
        }

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("location")]
        public string Location { get; set; } = string.Empty;

        [XmlAttribute("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = true)]
        [XmlAttribute("importType")]
        public string ImportType { get; set; } = string.Empty;
    }