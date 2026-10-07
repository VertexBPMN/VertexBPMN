// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record StandardLoopCharacteristics(string? LoopCondition, bool TestBefore, int? LoopMaximum) : LoopCharacteristics("standard");
