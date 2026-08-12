using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Parsing.Ecosystem;

/// <summary>
/// Phase 12: Streaming BPMN parser for extremely large files.
/// Uses SAX-style parsing to reduce memory footprint.
/// </summary>
public sealed class BpmnStreamingParser
{
    private readonly BpmnParserOptions _options;
    private readonly ILogger<BpmnStreamingParser>? _logger;

    public BpmnStreamingParser(): this(new BpmnParserOptions(), Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnStreamingParser>.Instance)
    {
    }
    public BpmnStreamingParser(BpmnParserOptions options, ILogger<BpmnStreamingParser>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Parses BPMN XML using streaming approach for large files.
    /// </summary>
    public async Task<BpmnModel> ParseAsync(string xml, CancellationToken cancellationToken = default)
    {
        // For files smaller than threshold, use standard parser
        if (xml.Length < _options.StreamingThreshold)
        {
            var standardParser = new BpmnParser(_options);
            return await standardParser.ParseAsync(xml, cancellationToken);
        }

        using var stringReader = new StringReader(xml);
        return await ParseStreamAsync(stringReader, cancellationToken);
    }

    /// <summary>
    /// Parses BPMN XML from a stream using SAX-style approach.
    /// </summary>
    public async Task<BpmnModel> ParseStreamAsync(Stream xmlStream, CancellationToken cancellationToken = default)
    {
        using var streamReader = new StreamReader(xmlStream);
        return await ParseStreamAsync(streamReader, cancellationToken);
    }

    /// <summary>
    /// Core streaming parse implementation.
    /// </summary>
    private async Task<BpmnModel> ParseStreamAsync(TextReader xmlReader, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Starting streaming BPMN parse with chunk size {ChunkSize} bytes", _options.StreamingChunkSize);
        
        var streamingContext = new StreamingParseContext();
        
        // Use XmlReader for streaming XML processing
        var xmlReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Async = true,
            CloseInput = false
        };

        using var xmlReader2 = XmlReader.Create(xmlReader, xmlReaderSettings);
        
        try
        {
            while (await xmlReader2.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (xmlReader2.NodeType == XmlNodeType.Element)
                {
                    await ProcessElementAsync(xmlReader2, streamingContext, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Streaming parse failed");
            throw;
        }
        
        // Build final model from streamed components
        var model = BuildModelFromStreamingContext(streamingContext);
        
        _logger?.LogDebug("Streaming parse completed: {ElementCount} elements processed", 
            streamingContext.TotalElementsProcessed);
        
        return model;
    }

    private async Task ProcessElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var localName = reader.LocalName;
        var namespaceUri = reader.NamespaceURI;
        
        // Process known BPMN elements
        switch (localName)
        {
            case "process":
                context.ProcessId = reader.GetAttribute("id") ?? string.Empty;
                break;
                
            case "startEvent":
            case "endEvent":
            case "intermediateCatchEvent":
            case "intermediateThrowEvent":
            case "boundaryEvent":
                await ProcessEventElementAsync(reader, context, cancellationToken);
                break;
                
            case var taskType when taskType.EndsWith("Task", StringComparison.OrdinalIgnoreCase):
            case "callActivity":
                await ProcessTaskElementAsync(reader, context, cancellationToken);
                break;
                
            case var gatewayType when gatewayType.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase):
                await ProcessGatewayElementAsync(reader, context, cancellationToken);
                break;
                
            case "sequenceFlow":
                await ProcessSequenceFlowElementAsync(reader, context, cancellationToken);
                break;
                
            case "subProcess":
            case "adHocSubProcess":
                await ProcessSubprocessElementAsync(reader, context, cancellationToken);
                break;
        }
        
        context.TotalElementsProcessed++;
    }

    private async Task ProcessEventElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        var type = reader.LocalName;
        var name = reader.GetAttribute("name");
        
        if (!string.IsNullOrEmpty(id))
        {
            // Read the full element for event definitions processing
            var element = await ReadElementAsync(reader, cancellationToken);
            var definitions = ParseEventDefinitions(element);
            
            context.Events.Add(new BpmnEvent(id, type, definitions, null, 
                name != null ? new Dictionary<string, string> { ["name"] = name } : null));
        }
    }

    private async Task ProcessTaskElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        var type = reader.LocalName;
        var name = reader.GetAttribute("name") ?? string.Empty;
        
        if (!string.IsNullOrEmpty(id))
        {
            // For streaming, we collect basic task info without full extension processing
            var task = new BpmnTask(id, type, null, null) { Name = name };
            
            // Process extensions if present - but limit depth for memory efficiency
            if (!reader.IsEmptyElement)
            {
                var extensions = await ProcessTaskExtensionsStreamingAsync(reader, cancellationToken);
                if (extensions.Count > 0)
                {
                    task = task with { Attributes = extensions };
                }
            }
            
            context.Tasks.Add(task);
        }
    }

    private async Task ProcessGatewayElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        var type = reader.LocalName;
        var defaultFlow = reader.GetAttribute("default");
        
        if (!string.IsNullOrEmpty(id))
        {
            context.Gateways.Add(new BpmnGateway(id, type, defaultFlow, null, null));
        }
    }

    private async Task ProcessSequenceFlowElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        var sourceRef = reader.GetAttribute("sourceRef") ?? string.Empty;
        var targetRef = reader.GetAttribute("targetRef") ?? string.Empty;
        var name = reader.GetAttribute("name");
        
        if (!string.IsNullOrEmpty(id))
        {
            // Read condition expression if present
            string? conditionExpression = null;
            if (!reader.IsEmptyElement)
            {
                var element = await ReadElementAsync(reader, cancellationToken);
                var conditionElement = element.Element(element.Name.Namespace + "conditionExpression");
                conditionExpression = conditionElement?.Value;
            }
            
            var extensions = name != null ? new Dictionary<string, string> { ["name"] = name } : null;
            context.SequenceFlows.Add(new BpmnSequenceFlow(id, sourceRef, targetRef, false, conditionExpression, null, extensions, null));
        }
    }

    private async Task ProcessSubprocessElementAsync(XmlReader reader, StreamingParseContext context, CancellationToken cancellationToken)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        var triggeredByEvent = reader.GetAttribute("triggeredByEvent") == "true";
        var transaction = reader.GetAttribute("transaction") == "true";
        
        if (!string.IsNullOrEmpty(id))
        {
            context.Subprocesses.Add(new BpmnSubprocess(id, triggeredByEvent, transaction, null, null, null));
        }
    }

    private async Task<XElement> ReadElementAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        // Read the current element as XElement for detailed processing
        return XElement.ReadFrom(reader) as XElement ?? throw new InvalidOperationException("Failed to read XML element");
    }

    private async Task<Dictionary<string, string>> ProcessTaskExtensionsStreamingAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var extensions = new Dictionary<string, string>();
        
        // Simplified extension processing for streaming mode
        // Full extension processing would require too much memory
        
        return extensions;
    }

    private IReadOnlyList<EventDefinition> ParseEventDefinitions(XElement eventElement)
    {
        // Reuse existing event definition parsing logic
        var definitions = new List<EventDefinition>();
        var ns = eventElement.Name.Namespace;
        
        foreach (var childElement in eventElement.Elements())
        {
            switch (childElement.Name.LocalName)
            {
                case "timerEventDefinition":
                    var timeDate = childElement.Element(ns + "timeDate")?.Value;
                    var timeDuration = childElement.Element(ns + "timeDuration")?.Value;
                    var timeCycle = childElement.Element(ns + "timeCycle")?.Value;
                    definitions.Add(new TimerEventDefinition(timeDate, timeDuration, timeCycle));
                    break;
                    
                case "messageEventDefinition":
                    var messageRef = childElement.Attribute("messageRef")?.Value ?? string.Empty;
                    var correlationKey = childElement.Attribute("correlationKey")?.Value;
                    definitions.Add(new MessageEventDefinition(messageRef, correlationKey));
                    break;
                    
                // Add other event definition types as needed
            }
        }
        
        return definitions;
    }

    private BpmnModel BuildModelFromStreamingContext(StreamingParseContext context)
    {
        // Build the final BPMN model from streaming context
        return new BpmnModel(
            ProcessId: context.ProcessId,
            Events: context.Events,
            Gateways: context.Gateways,
            Subprocesses: context.Subprocesses,
            SequenceFlows: context.SequenceFlows,
            Tasks: context.Tasks,
            DataObjects: Array.Empty<BpmnDataObject>(),
            DataObjectReferences: Array.Empty<BpmnDataObjectReference>(),
            DataStores: Array.Empty<BpmnDataStore>(),
            DataStoreReferences: Array.Empty<BpmnDataStoreReference>(),
            Properties: Array.Empty<BpmnProperty>(),
            ActivityIo: Array.Empty<BpmnActivityIo>(),
            Messages: Array.Empty<BpmnMessage>(),
            Signals: Array.Empty<BpmnSignal>(),
            Errors: Array.Empty<BpmnError>(),
            Escalations: Array.Empty<BpmnEscalation>(),
            Diagnostics: Array.Empty<string>(),
            Shapes: null,
            Edges: null,
            Participants: Array.Empty<BpmnParticipant>(),
            Lanes: Array.Empty<BpmnLane>(),
            MessageFlows: Array.Empty<BpmnMessageFlow>(),
            TextAnnotations: Array.Empty<BpmnTextAnnotation>(),
            Associations: Array.Empty<BpmnAssociation>(),
            Groups: Array.Empty<BpmnGroup>(),
            Activities: context.Tasks.Cast<object>().Concat(context.Subprocesses),
            RawMetadata: null
        );
    }

    /// <summary>
    /// Internal context for streaming parse operations.
    /// </summary>
    private sealed class StreamingParseContext
    {
        public string ProcessId { get; set; } = string.Empty;
        public List<BpmnEvent> Events { get; } = new();
        public List<BpmnTask> Tasks { get; } = new();
        public List<BpmnGateway> Gateways { get; } = new();
        public List<BpmnSubprocess> Subprocesses { get; } = new();
        public List<BpmnSequenceFlow> SequenceFlows { get; } = new();
        public int TotalElementsProcessed { get; set; }
    }
}