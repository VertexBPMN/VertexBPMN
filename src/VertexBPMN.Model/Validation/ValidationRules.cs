using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Validation;

public sealed record ValidationRuleDescriptor(
    string Code,
    string Category,
    ValidationSeverity DefaultSeverity,
    string Title,
    string Description);

public static class ValidationRules
{
    private static readonly ValidationRuleDescriptor[] _all = new[]
    {
        // Structural
        new ValidationRuleDescriptor("STR-DUP-ID", "Structural", ValidationSeverity.Error, "Duplicate ID", "Two or more elements share the same id."),
        new ValidationRuleDescriptor("STR-MISSING-PROCESS", "Structural", ValidationSeverity.Error, "Missing <process>", "No <process> element present in the BPMN definitions."),
        new ValidationRuleDescriptor("STR-MISSING-ID", "Structural", ValidationSeverity.Error, "Missing id attribute", "Flow node or sequence flow missing required id attribute."),
        // Referential
        new ValidationRuleDescriptor("REF-SEQUENCE-ENDPOINT", "Referential", ValidationSeverity.Error, "Invalid sequence flow endpoint", "SequenceFlow sourceRef or targetRef does not point to an existing flow node."),
        new ValidationRuleDescriptor("REF-BOUNDARY-ATTACHED-MISSING", "Referential", ValidationSeverity.Error, "Missing boundary attachment target", "Boundary event attachedToRef does not resolve to an existing activity."),
        new ValidationRuleDescriptor("REF-GLOBAL-MESSAGE-MISSING", "Referential", ValidationSeverity.Error, "Unknown messageRef", "Event references a message id that is not declared as a global <message>."),
        new ValidationRuleDescriptor("REF-GLOBAL-SIGNAL-MISSING", "Referential", ValidationSeverity.Error, "Unknown signalRef", "Event references a signal id that is not declared as a global <signal>."),
        new ValidationRuleDescriptor("REF-GLOBAL-ERROR-MISSING", "Referential", ValidationSeverity.Error, "Unknown errorRef", "Event references an error id that is not declared as a global <error>."),
        new ValidationRuleDescriptor("REF-GLOBAL-ESCALATION-MISSING", "Referential", ValidationSeverity.Error, "Unknown escalationRef", "Event references an escalation id that is not declared as a global <escalation>."),
        new ValidationRuleDescriptor("REF-LANE-FLOWNODE-MISSING", "Referential", ValidationSeverity.Warning, "Lane flowNodeRef missing", "Lane references a flow node id that does not exist."),
        new ValidationRuleDescriptor("REF-DATAOBJECTREF-TARGET-MISSING", "Referential", ValidationSeverity.Error, "Missing dataObjectRef target", "DataObjectReference points to a non-existent dataObject."),
        new ValidationRuleDescriptor("REF-ASSOCIATION-ENDPOINT-MISSING", "Referential", ValidationSeverity.Warning, "Association endpoint missing", "Association sourceRef or targetRef does not resolve."),
        // Semantic
        new ValidationRuleDescriptor("SEM-DEFAULT-WITH-CONDITION", "Semantic", ValidationSeverity.Error, "Default flow has condition", "A default sequence flow must not have a conditionExpression."),
        new ValidationRuleDescriptor("SEM-MI-CONFLICT", "Semantic", ValidationSeverity.Warning, "Multi-instance configuration conflict", "Loop cardinality and collection specified simultaneously."),
        new ValidationRuleDescriptor("SEM-LINK-UNMATCHED", "Semantic", ValidationSeverity.Error, "Unmatched link event", "Link throw event has no matching catch (or vice versa)."),
        new ValidationRuleDescriptor("SEM-LINK-MULTIPLE-THROW", "Semantic", ValidationSeverity.Error, "Multiple link throw events", "More than one throw link event shares the same name."),
        new ValidationRuleDescriptor("SEM-CANCEL-OUTSIDE-TX", "Semantic", ValidationSeverity.Warning, "Cancel end outside transaction", "Cancel end event not contained in a transaction subprocess."),
        new ValidationRuleDescriptor("SEM-TERMINATE-OUTSIDE-TX", "Semantic", ValidationSeverity.Warning, "Terminate end outside transaction", "Terminate end event not contained in a transaction subprocess."),
        new ValidationRuleDescriptor("SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY", "Semantic", ValidationSeverity.Error, "Compensation boundary must be non-interrupting", "Compensation boundary event must set cancelActivity='false'."),
        new ValidationRuleDescriptor("SEM-EVENTGW-INVALID-OUTGOING", "Semantic", ValidationSeverity.Error, "Invalid event-based gateway outgoing target", "Event-based gateway outgoing flow targets a non-catching intermediate event."),
        new ValidationRuleDescriptor("SEM-EVENTSUBPROCESS-START-TYPE", "Semantic", ValidationSeverity.Error, "Invalid event subprocess start type", "Event subprocess start event uses a disallowed event definition."),
        // Vendor/Extension
        new ValidationRuleDescriptor("VEN-UNKNOWN-EVENT-DEFINITION", "Vendor", ValidationSeverity.Info, "Unknown event definition", "Event contains a vendor-specific or unknown event definition that will be preserved in raw form."),
        // Advisory
        new ValidationRuleDescriptor("ADV-UNREACHABLE-NODE", "Advisory", ValidationSeverity.Info, "Unreachable node", "Flow node not reachable from any root start event."),
        new ValidationRuleDescriptor("ADV-ORPHANED-END", "Advisory", ValidationSeverity.Info, "Orphaned end event", "End event not reachable from any root start event."),
        new ValidationRuleDescriptor("ADV-DEAD-SEQUENCE-FLOW", "Advisory", ValidationSeverity.Info, "Dead sequence flow", "Sequence flow endpoints are not both reachable.")
    };

    public static IReadOnlyList<ValidationRuleDescriptor> All { get; } =
        new ReadOnlyCollection<ValidationRuleDescriptor>(_all);

    public static IReadOnlyDictionary<string, ValidationRuleDescriptor> ByCode { get; } =
        new ReadOnlyDictionary<string, ValidationRuleDescriptor>(
            _all.ToDictionary(r => r.Code, r => r, StringComparer.Ordinal));

    public static bool TryGet(string code, out ValidationRuleDescriptor descriptor) =>
        ByCode.TryGetValue(code, out descriptor);
}