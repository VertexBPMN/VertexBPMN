// Temporary placeholder entity type stubs to satisfy generated code references.
// TODO: Replace with proper generated implementations once EntityGenerator emits them.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Xml;

namespace VertexBPMN.Domain.Model.Bpmn.Entities
{
 public class FlowElementEntity { public Guid Id { get; set; } }
 public class ArtifactEntity { public Guid Id { get; set; } }
 public class EventDefinitionEntity { public Guid Id { get; set; } }
 public class GatewayDirectionEntity { public int Value { get; set; } }
 public class AdHocOrderingEntity { public int Value { get; set; } }
 public class LoopCharacteristicsEntity { public Guid Id { get; set; } }
 public class AssociationDirectionEntity { public int Value { get; set; } }
 public class MessageVisibleKindEntity { public int Value { get; set; } }
 public class ParticipantBandKindEntity { public int Value { get; set; } }
 public class ChoreographyLoopTypeEntity { public int Value { get; set; } }
 public class DiagramElementEntity { public Guid Id { get; set; } }
 public class ConversationNodeEntity { public Guid Id { get; set; } }
 public class RootElementEntity { public Guid Id { get; set; } }
 public class MultiInstanceFlowConditionEntity { public int Value { get; set; } }
 public class ProcessTypeEntity { public int Value { get; set; } }
 public class RelationshipDirectionEntity { public int Value { get; set; } }
 public class ItemKindEntity { public int Value { get; set; } }
 public class BaseElementEntity { public Guid Id { get; set; } }
 // XML element placeholder collections
 public class XmlElementCollectionEntity { public List<XmlElement> Items { get; set; } = new(); }
}
