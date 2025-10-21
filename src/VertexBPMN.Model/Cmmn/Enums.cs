using System;

namespace VertexBPMN.Domain.Model.Cmmn;

#nullable enable

/// <summary>
/// Enum for definition types in CaseFileItemDefinition.
/// </summary>
public enum DefinitionType
{
    Unspecified,
    CmisDocument,
    CmisFolder,
    CmisRelationship,
    XsdElement,
    XsdComplexType,
    XsdSimpleType,
    Unknown
}

/// <summary>
/// Enum for property types in Property.
/// </summary>
public enum PropertyType
{
    Unspecified,
    String,
    Integer,
    Float,
    Double,
    Boolean,
    DateTime,
    Date,
    Time,
    Duration,
    HexBinary,
    Base64Binary,
    AnyUri,
    QName,
    Decimal,
    GYearMonth,
    GYear,
    GMonthDay,
    GDay,
    GMonth
}

/// <summary>
/// Enum for multiplicity in CaseFileItem.
/// </summary>
public enum MultiplicityEnum
{
    ZeroOrOne,
    ExactlyOne,
    ZeroOrMore,
    Unbounded
}

/// <summary>
/// Enum for relationship directions.
/// </summary>
public enum RelationshipDirection
{
    None,
    Forward,
    Backward,
    Both
}

/// <summary>
/// Enum for standard events in CaseFileItemTransition.
/// </summary>
public enum CaseFileItemTransition
{
    Create,
    AddChild,
    RemoveChild,
    AddReference,
    RemoveReference,
    Replace,
    Update,
    Delete
}

/// <summary>
/// Enum for standard events in PlanItemTransition.
/// </summary>
public enum PlanItemTransition
{
    Create,
    Initiate,
    ManualStart,
    Start,
    Suspend,
    Resume,
    Reactivate,
    Complete,
    Terminate,
    ParentResume,
    ParentSuspend,
    ParentTerminate,
    Exit,
    Occur,
    Fault,
    Close
}

/// <summary>
/// Enum for CaseFileItem states (Clause 8.3).
/// </summary>
public enum CaseFileItemState
{
    Available,
    Discarded
}

/// <summary>
/// Enum for Case instance states (Clause 8.1, Figure 8.2).
/// </summary>
public enum CaseState
{
    Active,
    Suspended,
    Completed,
    Terminated,
    Failed,
    Closed
}

/// <summary>
/// Enum for Stage/Task states (Clause 8.4, Figure 8.3).
/// </summary>
public enum PlanItemState
{
    Available,
    Enabled,
    Disabled,
    Active,
    Suspended,
    Failed,
    Completed,
    Terminated
}

/// <summary>
/// Enum for EventListener/Milestone states (Clause 8.6, Figure 8.4).
/// </summary>
public enum EventMilestoneState
{
    Available,
    Suspended,
    Completed,
    Terminated
}
