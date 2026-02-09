namespace VertexBPMN.Domain.Model.Bpmn.Common;

public enum AssociationDirection
{
    None,
    One,
    Both
}

public enum RelationshipDirection
{
    None,
    Forward,
    Backward,
    Both
}

public enum ItemKind
{
    Information,
    Physical
}

public enum GatewayDirection
{
    Unspecified,
    Converging,
    Diverging,
    Mixed
}