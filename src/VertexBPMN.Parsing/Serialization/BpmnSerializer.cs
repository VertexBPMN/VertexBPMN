using System;
using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Parsing.Serialization;

/// <summary>
/// Basic BPMN serializer for existing roundtrip functionality.
/// Phase 8: This is kept for backward compatibility while NormalizedProjectionSerializer handles new scenarios.
/// </summary>
public class BpmnSerializer
{
    /// <summary>
    /// Roundtrip mode for serialization behavior.
    /// </summary>
    public BpmnRoundtripMode RoundtripMode { get; set; } = BpmnRoundtripMode.Normalized;

    /// <summary>
    /// Serializes a BPMN model to XML string.
    /// </summary>
    public string Serialize(BpmnModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var ns = XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/MODEL");
        var definitions = new XElement(ns + "definitions",
            new XAttribute("xmlns", "http://www.omg.org/spec/BPMN/20100524/MODEL"));

        var process = new XElement(ns + "process", new XAttribute("id", model.ProcessId));

        // Add events
        foreach (var evt in model.Events)
        {
            var eventElement = new XElement(ns + evt.Type, new XAttribute("id", evt.Id));
            
            if (!string.IsNullOrEmpty(evt.Name))
                eventElement.Add(new XAttribute("name", evt.Name));
            
            process.Add(eventElement);
        }

        // Add tasks
        foreach (var task in model.Tasks)
        {
            var taskElement = new XElement(ns + task.Type, new XAttribute("id", task.Id));
            
            if (!string.IsNullOrEmpty(task.Name))
                taskElement.Add(new XAttribute("name", task.Name));
            
            process.Add(taskElement);
        }

        // Add gateways
        foreach (var gateway in model.Gateways)
        {
            var gatewayElement = new XElement(ns + gateway.Type, new XAttribute("id", gateway.Id));
            
            if (!string.IsNullOrEmpty(gateway.DefaultFlowId))
                gatewayElement.Add(new XAttribute("default", gateway.DefaultFlowId));
            
            process.Add(gatewayElement);
        }

        // Add sequence flows
        foreach (var flow in model.SequenceFlows)
        {
            var flowElement = new XElement(ns + "sequenceFlow",
                new XAttribute("id", flow.Id),
                new XAttribute("sourceRef", flow.SourceRef),
                new XAttribute("targetRef", flow.TargetRef));

            if (!string.IsNullOrEmpty(flow.Name))
                flowElement.Add(new XAttribute("name", flow.Name));

            if (!string.IsNullOrEmpty(flow.ConditionExpression))
            {
                var conditionElement = new XElement(ns + "conditionExpression", new XCData(flow.ConditionExpression));
                flowElement.Add(conditionElement);
            }

            process.Add(flowElement);
        }

        definitions.Add(process);

        // Add global elements if present
        foreach (var message in model.Messages)
        {
            var messageElement = new XElement(ns + "message", new XAttribute("id", message.Id));
            if (!string.IsNullOrEmpty(message.Name))
                messageElement.Add(new XAttribute("name", message.Name));
            definitions.Add(messageElement);
        }

        foreach (var signal in model.Signals)
        {
            var signalElement = new XElement(ns + "signal", new XAttribute("id", signal.Id));
            if (!string.IsNullOrEmpty(signal.Name))
                signalElement.Add(new XAttribute("name", signal.Name));
            definitions.Add(signalElement);
        }

        return definitions.ToString(SaveOptions.DisableFormatting);
    }
}