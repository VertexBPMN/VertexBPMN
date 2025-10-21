using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

/// <summary>
/// Basic BPMN serializer for existing roundtrip functionality.
/// This is kept for backward compatibility while NormalizedProjectionSerializer handles new scenarios.
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

            if (evt.Definitions.Any())
            {
                var elements = new List<XElement>();
                foreach (var def in evt.Definitions)
                {
                    if (def is MessageEventDefinition m && !string.IsNullOrEmpty(m.MessageRef))
                    {
                        elements.Add(new XElement(ns + "messageRef", m.MessageRef));
                    }
                    else if (def is SignalEventDefinition s && !string.IsNullOrEmpty(s.SignalRef))
                    {
                        var signalElement = new XElement(ns + "signalEventDefinition");
                        if (!string.IsNullOrEmpty(s.SignalRef))
                            signalElement.Add(new XAttribute("signalRef", s.SignalRef));
                        elements.Add(signalElement);
                    }
                    else if (def is ErrorEventDefinition e && !string.IsNullOrEmpty(e.ErrorRef))
                    {
                        var el = new XElement(ns + "ErrorEventDefinition");
                        if (!string.IsNullOrEmpty(e.ErrorRef))
                            el.Add(new XAttribute("errorRef", e.ErrorRef));
                        elements.Add(el);
                    }
                    else if (def is EscalationEventDefinition es && !string.IsNullOrEmpty(es.EscalationRef))
                    {
                        var el = new XElement(ns + "escalationEventDefinition");
                        if (!string.IsNullOrEmpty(es.EscalationRef))
                            el.Add(new XAttribute("escalationRef", es.EscalationRef));
                        elements.Add(el);
                    }
                    else if (def is TimerEventDefinition t)
                    {
                        var timerElement = new XElement(ns + "timerEventDefinition");
                         if (!string.IsNullOrEmpty(t.TimeDuration))
                                timerElement.Add(new XElement(ns + "timeDuration", new XCData(t.TimeDuration)));
                         else if (!string.IsNullOrEmpty(t.TimeDate))
                                timerElement.Add(new XElement(ns + "timeDate", new XCData(t.TimeDate)));
                         else if (!string.IsNullOrEmpty(t.TimeCycle))
                                timerElement.Add(new XElement(ns + "timeCycle", new XCData(t.TimeCycle)));
                        
                        elements.Add(timerElement);
                    }
                    else if (def is ConditionalEventDefinition c && !string.IsNullOrEmpty(c.Condition))
                    {
                        var conditionalElement = new XElement(ns + "conditionalEventDefinition");
                        conditionalElement.Add(new XElement(ns + "condition", new XCData(c.Condition)));
                        elements.Add(conditionalElement);
                    }
                    else if (def is TerminateEventDefinition)
                    {
                        elements.Add(new XElement(ns + "terminateEventDefinition"));
                    }
                    else if (def is CompensationEventDefinition comp)
                    {
                        var compensateElement = new XElement(ns + "compensateEventDefinition");
                        if (!string.IsNullOrEmpty(comp.ActivityRef))
                            compensateElement.Add(new XAttribute("activityRef", comp.ActivityRef));
                        if (comp.WaitForCompletion?.Any() == true)
                            if (comp.WaitForCompletion?.Any() == true)
                            {
                                foreach (var co in comp.WaitForCompletion)
                                    compensateElement.Add(new XAttribute("waitForCompletion", co.ToString().ToLowerInvariant()));
                            }
                       
                        elements.Add(compensateElement);
                    }
                    else if (def is LinkEventDefinition l)
                    {
                        var linkElement = new XElement(ns + "linkEventDefinition");
                        if (!string.IsNullOrEmpty(l.Name))
                            linkElement.Add(new XAttribute("name", l.Name));
                        if (l.Sources?.Any() == true)
                        {
                            foreach (var source in l.Sources)
                                linkElement.Add(new XElement(ns + "source", source));
                        }
                        if (!string.IsNullOrEmpty(l.Target))
                            linkElement.Add(new XElement(ns + "target", l.Target));
                        elements.Add(linkElement);
                    }
                    else if (def is CancelEventDefinition)
                    {
                        elements.Add(new XElement(ns + "cancelEventDefinition"));
                    }
                }
                eventElement.Add(elements);
            }
            
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