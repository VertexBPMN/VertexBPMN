using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

namespace VertexBPMN.Domain.Model.Bpmn.Validation;

/// <summary>
/// Central registry for semantic rules (execution order matters for short-circuit if desired).
/// </summary>
internal static class SemanticRules
{
    public static IReadOnlyList<IBpmnSemanticRule> All { get; } =
        new IBpmnSemanticRule[]
        {
            new StartEndAndReachabilityRule(),
            new SequenceFlowConditionsRule(),
            new BoundaryAndCompensationRule(),
            new DataReferenceRule(),
            new TimerEventExclusivityRule(),
            new DiagramConsistencyRule(),
            new DefinitionUniquenessRule(),
            // Newly added extended rules
            new GatewayBranchingRule(),
            new SubProcessMultiplicityAndTriggerRule(),
            new TaskImplementationAndReferenceRule(),
            new MessageFlowParticipantRule(),
            new LaneMembershipRule(),
            new AssociationValidityRule(),
            new EventDefinitionReferenceRule(),
            new ConditionalAndDefaultFlowRule(),
            new UniqueMessageSignalErrorEscalationRule(),
            new InterruptingStartEventContextRule(),
            new TerminateAndCancelEventRule(),
            new FormalExpressionNonEmptyRule()
        };
}