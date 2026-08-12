using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Ecosystem;

/// <summary>
/// Policy-based redaction processor for BPMN content.
/// Strips confidential or sensitive information based on configurable policies.
/// </summary>
public sealed class BpmnRedactionProcessor
{
    private readonly BpmnRedactionPolicies _policies;
    private readonly ILogger<BpmnRedactionProcessor>? _logger;

    public BpmnRedactionProcessor(BpmnRedactionPolicies policies):this(policies, Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnRedactionProcessor>.Instance)
    {
    }


    public BpmnRedactionProcessor(BpmnRedactionPolicies policies, ILogger<BpmnRedactionProcessor>? logger = null)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _logger = logger;
    }

    /// <summary>
    /// Applies redaction policies to a BPMN model.
    /// </summary>
    public BpmnModel ApplyRedaction(BpmnModel model)
    {
        if (!_policies.StripConfidentialData)
            return model;

        _logger?.LogDebug("Applying redaction policies to BPMN model {ProcessId}", model.ProcessId);

        var redactionStats = new RedactionStatistics();

        // Apply redaction to different model components
        var redactedTasks = ApplyTaskRedaction(model.Tasks, redactionStats);
        var redactedEvents = ApplyEventRedaction(model.Events, redactionStats);
        var redactedSubprocesses = ApplySubprocessRedaction(model.Subprocesses, redactionStats);
        var redactedFlows = ApplySequenceFlowRedaction(model.SequenceFlows, redactionStats);
        var redactedRawMetadata = ApplyRawMetadataRedaction(model.RawMetadata, redactionStats);

        var redactedModel = model with
        {
            Tasks = redactedTasks,
            Events = redactedEvents,
            Subprocesses = redactedSubprocesses,
            SequenceFlows = redactedFlows,
            RawMetadata = redactedRawMetadata
        };

        _logger?.LogInformation("Redaction completed: {RedactedAttributes} attributes, {RedactedElements} elements, {RedactedNamespaces} namespaces processed",
            redactionStats.RedactedAttributeCount, redactionStats.RedactedElementCount, redactionStats.RedactedNamespaceCount);

        return redactedModel;
    }

    private IReadOnlyList<BpmnTask> ApplyTaskRedaction(IReadOnlyList<BpmnTask> tasks, RedactionStatistics stats)
    {
        var redactedTasks = new List<BpmnTask>();

        foreach (var task in tasks)
        {
            var redactedAttributes = RedactAttributes(task.Attributes, stats);
            var redactedTask = task with { Attributes = (Dictionary<string, string>)redactedAttributes };
            redactedTasks.Add(redactedTask);
        }

        return redactedTasks;
    }

    private IReadOnlyList<BpmnEvent> ApplyEventRedaction(IReadOnlyList<BpmnEvent> events, RedactionStatistics stats)
    {
        var redactedEvents = new List<BpmnEvent>();

        foreach (var evt in events)
        {
            var redactedExtensions = RedactAttributes(evt.Attributes, stats);
            var redactedEvent = evt with { Attributes = (Dictionary<string, string>)redactedExtensions };
            redactedEvents.Add(redactedEvent);
        }

        return redactedEvents;
    }

    private IReadOnlyList<BpmnSubprocess> ApplySubprocessRedaction(IReadOnlyList<BpmnSubprocess> subprocesses, RedactionStatistics stats)
    {
        var redactedSubprocesses = new List<BpmnSubprocess>();

        foreach (var subprocess in subprocesses)
        {
            var redactedExtensions = RedactAttributes(subprocess.Attributes, stats);
            var redactedSubprocess = subprocess with { Attributes = (Dictionary<string, string>)redactedExtensions };
            redactedSubprocesses.Add(redactedSubprocess);
        }

        return redactedSubprocesses;
    }

    private IReadOnlyList<BpmnSequenceFlow> ApplySequenceFlowRedaction(IReadOnlyList<BpmnSequenceFlow> flows, RedactionStatistics stats)
    {
        var redactedFlows = new List<BpmnSequenceFlow>();

        foreach (var flow in flows)
        {
            var redactedExtensions = RedactAttributes(flow.Attributes, stats);
            var redactedFlow = flow with { Attributes = (Dictionary<string, string>)redactedExtensions };
            redactedFlows.Add(redactedFlow);
        }

        return redactedFlows;
    }

    private BpmnRawMetadata? ApplyRawMetadataRedaction(BpmnRawMetadata? metadata, RedactionStatistics stats)
    {
        if (metadata == null)
            return null;

        // Redact documentation if configured
        var redactedDocumentation = metadata.RawDocumentation;
        if (_policies.RedactedElements.Contains("documentation") && redactedDocumentation != null)
        {
            redactedDocumentation = null;
            stats.RedactedElementCount++;
        }

        // Redact raw extensions based on namespace policies
        var redactedExtensions = metadata.RawExtensionElements;
        if (redactedExtensions != null && _policies.RedactedNamespaces.Count > 0)
        {
            var filteredExtensions = new Dictionary<string, XElement>();
            
            foreach (var kvp in redactedExtensions)
            {
                var filteredElement = RedactXElement(kvp.Value, stats);
                if (filteredElement.HasElements || filteredElement.HasAttributes)
                {
                    filteredExtensions[kvp.Key] = filteredElement;
                }
            }
            
            redactedExtensions = filteredExtensions.Count > 0 ? filteredExtensions : null;
        }

        return metadata with
        {
            RawDocumentation = redactedDocumentation,
            RawExtensionElements = redactedExtensions
        };
    }

    private IReadOnlyDictionary<string, string>? RedactAttributes(IReadOnlyDictionary<string, string>? attributes, RedactionStatistics stats)
    {
        if (attributes == null || attributes.Count == 0)
            return attributes;

        var filteredAttributes = new Dictionary<string, string>();

        foreach (var kvp in attributes)
        {
            var shouldRedact = false;

            // Check if attribute should be redacted
            foreach (var redactedAttr in _policies.RedactedAttributes)
            {
                if (kvp.Key.Contains(redactedAttr, StringComparison.OrdinalIgnoreCase))
                {
                    shouldRedact = true;
                    break;
                }
            }

            // Check if attribute is explicitly preserved
            if (shouldRedact)
            {
                foreach (var preservedAttr in _policies.PreserveAttributes)
                {
                    if (kvp.Key.Contains(preservedAttr, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldRedact = false;
                        break;
                    }
                }
            }

            // Check namespace-based redaction
            if (!shouldRedact)
            {
                foreach (var redactedNs in _policies.RedactedNamespaces)
                {
                    if (kvp.Key.StartsWith(GetNamespacePrefix(redactedNs), StringComparison.OrdinalIgnoreCase))
                    {
                        shouldRedact = true;
                        stats.RedactedNamespaceCount++;
                        break;
                    }
                }
            }

            if (shouldRedact)
            {
                stats.RedactedAttributeCount++;
                _logger?.LogTrace("Redacted attribute: {AttributeKey}", kvp.Key);
            }
            else
            {
                filteredAttributes[kvp.Key] = kvp.Value;
            }
        }

        return filteredAttributes.Count > 0 ? filteredAttributes : null;
    }

    private XElement RedactXElement(XElement element, RedactionStatistics stats)
    {
        var redactedElement = new XElement(element.Name);

        // Copy attributes that aren't redacted
        foreach (var attr in element.Attributes())
        {
            var shouldRedactAttr = _policies.RedactedAttributes.Any(ra => 
                attr.Name.LocalName.Contains(ra, StringComparison.OrdinalIgnoreCase));

            if (!shouldRedactAttr)
            {
                redactedElement.Add(new XAttribute(attr));
            }
            else
            {
                stats.RedactedAttributeCount++;
            }
        }

        // Process child elements
        foreach (var child in element.Elements())
        {
            var shouldRedactElement = _policies.RedactedElements.Contains(child.Name.LocalName) ||
                                    _policies.RedactedNamespaces.Contains(child.Name.NamespaceName);

            if (!shouldRedactElement)
            {
                redactedElement.Add(RedactXElement(child, stats));
            }
            else
            {
                stats.RedactedElementCount++;
            }
        }

        // Copy text content if not in a redacted element
        if (element.HasElements == false && !string.IsNullOrWhiteSpace(element.Value))
        {
            redactedElement.Value = element.Value;
        }

        return redactedElement;
    }

    private static string GetNamespacePrefix(string namespaceUri)
    {
        // Simple mapping of common namespaces to prefixes
        return namespaceUri switch
        {
            "http://camunda.org/schema/1.0/bpmn" => "camunda:",
            "http://zeebe.io/schema/zeebe/1.0" => "zeebe:",
            "http://flowable.org/bpmn" => "flowable:",
            "http://activiti.org/bpmn" => "activiti:",
            _ => "unknown:"
        };
    }

    /// <summary>
    /// Statistics about redaction operations.
    /// </summary>
    private sealed class RedactionStatistics
    {
        public int RedactedAttributeCount { get; set; }
        public int RedactedElementCount { get; set; }
        public int RedactedNamespaceCount { get; set; }
    }
}