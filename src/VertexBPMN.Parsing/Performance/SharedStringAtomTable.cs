using System;
using System.Collections.Concurrent;

namespace VertexBPMN.Parsing.Performance;

/// <summary>
/// Shared atom table for interning common BPMN strings to reduce memory usage.
/// Thread-safe singleton for use across multiple parser instances.
/// </summary>
public static class SharedStringAtomTable
{
    // Common BPMN element names
    private static readonly ConcurrentDictionary<string, string> _commonAtoms = new(StringComparer.Ordinal);
    
    // Pre-populate with common BPMN terms
    static SharedStringAtomTable()
    {
        var commonStrings = new[]
        {
            // Element types
            "startEvent", "endEvent", "userTask", "serviceTask", "scriptTask", "manualTask",
            "receiveTask", "sendTask", "businessRuleTask", "callActivity", "subProcess",
            "exclusiveGateway", "parallelGateway", "inclusiveGateway", "eventBasedGateway",
            "complexGateway", "sequenceFlow", "messageFlow", "association",
            "intermediateCatchEvent", "intermediateThrowEvent", "boundaryEvent",
            
            // Common attribute names
            "id", "name", "sourceRef", "targetRef", "processRef", "attachedToRef",
            "default", "isSequential", "cancelActivity", "scriptFormat", "resultVariable",
            
            // Common attribute values
            "true", "false", "javascript", "groovy", "python", "juel",
            
            // Namespace URIs (most common)
            "http://www.omg.org/spec/BPMN/20100524/MODEL",
            "http://camunda.org/schema/1.0/bpmn",
            "http://zeebe.io/schema/zeebe/1.0",
            "http://flowable.org/bpmn",
            "http://activiti.org/bpmn",
            
            // Common prefixes
            "bpmn", "camunda", "zeebe", "flowable", "activiti",
            
            // Event definition types  
            "messageEventDefinition", "timerEventDefinition", "signalEventDefinition",
            "errorEventDefinition", "escalationEventDefinition", "conditionalEventDefinition",
            "cancelEventDefinition", "compensateEventDefinition", "terminateEventDefinition",
            "linkEventDefinition"
        };
        
        foreach (var str in commonStrings)
        {
            _commonAtoms.TryAdd(str, str);
        }
    }
    
    /// <summary>
    /// Interns a string using the shared atom table. Returns the canonical instance.
    /// For common BPMN terms, this reduces memory usage significantly.
    /// </summary>
    public static string Intern(string value)
    {
        if (string.IsNullOrEmpty(value)) 
            return value;
        
        // For very long strings, don't intern (avoid memory leaks)
        if (value.Length > 200) 
            return value;
        
        return _commonAtoms.GetOrAdd(value, value);
    }
    
    /// <summary>
    /// Gets the current size of the atom table (for diagnostics/testing).
    /// </summary>
    public static int Count => _commonAtoms.Count;
    
    /// <summary>
    /// Clears the dynamic entries (keeps pre-populated common terms).
    /// Useful for testing to avoid state leakage.
    /// </summary>
    public static void ClearDynamicEntries()
    {
        var commonStrings = new[]
        {
            "startEvent", "endEvent", "userTask", "serviceTask", "scriptTask", "manualTask",
            "receiveTask", "sendTask", "businessRuleTask", "callActivity", "subProcess",
            "exclusiveGateway", "parallelGateway", "inclusiveGateway", "eventBasedGateway",
            "complexGateway", "sequenceFlow", "messageFlow", "association",
            "intermediateCatchEvent", "intermediateThrowEvent", "boundaryEvent",
            "id", "name", "sourceRef", "targetRef", "processRef", "attachedToRef",
            "default", "isSequential", "cancelActivity", "scriptFormat", "resultVariable",
            "true", "false", "javascript", "groovy", "python", "juel",
            "http://www.omg.org/spec/BPMN/20100524/MODEL",
            "http://camunda.org/schema/1.0/bpmn",
            "http://zeebe.io/schema/zeebe/1.0",
            "http://flowable.org/bpmn",
            "http://activiti.org/bpmn",
            "bpmn", "camunda", "zeebe", "flowable", "activiti",
            "messageEventDefinition", "timerEventDefinition", "signalEventDefinition",
            "errorEventDefinition", "escalationEventDefinition", "conditionalEventDefinition",
            "cancelEventDefinition", "compensateEventDefinition", "terminateEventDefinition",
            "linkEventDefinition"
        };
        
        _commonAtoms.Clear();
        foreach (var str in commonStrings)
        {
            _commonAtoms.TryAdd(str, str);
        }
    }
}