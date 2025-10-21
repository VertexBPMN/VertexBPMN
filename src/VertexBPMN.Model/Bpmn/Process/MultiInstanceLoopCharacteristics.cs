using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Multi instance loop characteristics, as per Figure 10.45.
/// </summary>
public record MultiInstanceLoopCharacteristics(
    Expression LoopCardinality,
    bool IsSequential = false,
    MultiInstanceBehavior Behavior = MultiInstanceBehavior.All,
    Expression? CompletionCondition = null,
    ItemAwareElement? LoopDataInputRef = null,
    ItemAwareElement? LoopDataOutputRef = null,
    List<InputDataItem>? InputDataItem = null,
    List<OutputDataItem>? OutputDataItem = null,
    List<ComplexBehaviorDefinition>? ComplexBehaviorDefinitions = null,
    ItemAwareElement? OneBehaviorEventRef = null,
    ItemAwareElement? NoneBehaviorEventRef = null
) : LoopCharacteristics;