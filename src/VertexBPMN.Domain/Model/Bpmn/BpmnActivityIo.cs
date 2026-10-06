// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnActivityIo(string ActivityId, IReadOnlyList<BpmnDataInput> DataInputs, IReadOnlyList<BpmnDataOutput> DataOutputs, IReadOnlyList<BpmnDataAssociation> InputAssociations, IReadOnlyList<BpmnDataAssociation> OutputAssociations);
