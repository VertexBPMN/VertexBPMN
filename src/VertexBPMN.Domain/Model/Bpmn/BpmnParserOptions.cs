using Microsoft.Extensions.Logging;
using System.Diagnostics;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Security;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Options controlling parsing performance and validation behavior.
/// </summary>
public sealed class BpmnParserOptions
{
    /// <summary>Enable strict validation diagnostics (default true).</summary>
    public bool StrictValidation { get; set; } = true;
    /// <summary>Preserve unknown vendor extension elements/attributes (default true).</summary>
    public bool PreserveUnknownExtensions { get; set; } = true;
    /// <summary>Maximum number of cached parsed models (0 disables caching).</summary>
    public int CacheSize { get; set; } = 0;
    /// <summary>Parse BPMN Diagram Interchange (shapes/edges) when true.</summary>
    public bool ParseDiagramInterchange { get; set; } = false;
    /// <summary>
    /// Roundtrip mode: Normalized (current default behaviour) vs Strict (attempt lossless preservation).
    /// </summary>
    public BpmnRoundtripMode RoundtripMode { get; set; } = BpmnRoundtripMode.Normalized;
    /// <summary>
    /// If true (default), parser applies memory optimizations in strict mode (release empty raw maps, selective cloning, interning IDs).
    /// </summary>
    public bool OptimizeStrictMemory { get; set; } = true;
    /// <summary>
    /// If true (default), short identifiers (id, sourceRef, targetRef) and flow refs are interned via a small in-memory pool to reduce duplicates.
    /// </summary>
    public bool InternIds { get; set; } = true;
    /// <summary>
    /// Strict-mode capture switch: record raw artifact elements (textAnnotation, association, group). Default true for backward compatibility.
    /// </summary>
    public bool CaptureArtifacts { get; set; } = true;
    /// <summary>
    /// Strict-mode capture switch: record raw Diagram Interchange root (BPMNDiagram parent) if present. Default true.
    /// </summary>
    public bool CaptureDiRaw { get; set; } = true;
    /// <summary>
    /// Enable parsing of collaboration section (participants, messageFlow). Default false (Phase 1 – zero-break).
    /// </summary>
    public bool EnableCollaborationParsing { get; set; } = false;
    /// <summary>
    /// Build a lightweight index of global element kinds (message/signal/error/escalation). Default false (Phase 1 opt-in).
    /// </summary>
    public bool BuildGlobalElementIndex { get; set; } = false;
    /// <summary>
    /// Phase 2: Enable vendor extension normalization (flatten known camunda / zeebe / flowable / cib / jbpm / osmanthus / alfresco / mcp patterns).
    /// Default false (zero-break).
    /// </summary>
    public bool NormalizeVendorExtensions { get; set; } = false;

    // (generische/unbekannte Namespaces – zusätzlich)
    public bool NormalizeUnknownVendorExtensions { get; set; } = false;
    /// <summary>
    /// Enable advanced structured validation diagnostics (incremental rules).
    /// Zero-break: off by default.
    /// </summary>
    public bool EnableAdvancedValidation { get; set; } = false;
    /// <summary>
    /// When true, parsing throws if a structured validation diagnostic exists
    /// with Severity >= MinimumThrowSeverity (only applies when EnableAdvancedValidation = true).
    /// Default false (zero-break).
    /// </summary>
    public bool ThrowOnFatalValidation { get; set; } = false;
    /// <summary>
    /// Minimum severity that triggers an exception when ThrowOnFatalValidation is true.
    /// Default = Error.
    /// </summary>
    public ValidationSeverity MinimumThrowSeverity { get; set; } = ValidationSeverity.Error;
    public bool BuildRuntimeProjection { get; set; } = false;

    // Phase 5: Observability Integration (NEW)
    /// <summary>
    /// Enable OpenTelemetry tracing for parse operations. Default false (zero-break).
    /// When enabled, creates spans with process metrics and timing.
    /// </summary>
    public bool EnableTracing { get; set; } = false;
    
    /// <summary>
    /// ActivitySource for creating tracing spans. If null and EnableTracing=true, uses default source.
    /// </summary>
    public ActivitySource? TracingActivitySource { get; init; }
    
    /// <summary>
    /// Enable structured logging at key parse stages. Default false (zero-break).
    /// When enabled, emits ParseStart, PhaseComplete, ValidationSummary, ProjectionBuilt messages.
    /// </summary>
    public bool EnableLogging { get; set; } = false;
    
    /// <summary>
    /// Logger instance for structured logging. If null and EnableLogging=true, uses NullLogger.
    /// </summary>
    public ILogger? Logger { get; set; }
    
    // Phase 6: Performance & Memory Layer (NEW)
    /// <summary>
    /// Use shared string atom table for interning common BPMN terms across parser instances.
    /// Reduces memory usage but may add slight CPU overhead. Default true.
    /// </summary>
    public bool UseSharedStringPool { get; set; } = true;
    
    /// <summary>
    /// Use ArrayPool for temporary collections during parsing to reduce GC pressure.
    /// Default true for better performance in high-throughput scenarios.
    /// </summary>    
    public bool UseArrayPooling { get; set; } = true;
    
    /// <summary>
    /// Use lazy cloning for raw extension elements to defer memory allocation until access.
    /// Only applies in Strict mode. Default true.
    /// </summary>
    public bool UseLazyRawCloning { get; set; } = true;

    // Phase 6: Large Model Optimizations (NEW - Zero-Break Default Off)
    /// <summary>
    /// Enable automatic optimizations for large models (>LargeModelThreshold elements).
    /// When enabled, parser applies memory-saving strategies for models that exceed the threshold.
    /// Default false (zero-break).
    /// </summary>
    public bool OptimizeLargeModels { get; set; } = false;

    /// <summary>
    /// Element count threshold for triggering large model optimizations.
    /// Models with more elements than this threshold will use optimized parsing strategies.
    /// Only applies when OptimizeLargeModels=true. Default 100.
    /// </summary>
    public int LargeModelThreshold { get; set; } = 100;

    /// <summary>
    /// Skip raw documentation capture for large models to reduce memory usage.
    /// Only applies when OptimizeLargeModels=true and threshold is exceeded. Default false (zero-break).
    /// </summary>
    public bool SkipDocumentationForLargeModels { get; set; } = false;

    /// <summary>
    /// Skip raw artifact capture (textAnnotation, association, group) for large models.
    /// Only applies when OptimizeLargeModels=true and threshold is exceeded. Default false (zero-break).
    /// </summary>
    public bool SkipArtifactsForLargeModels { get; set; } = false;

    /// <summary>
    /// Skip raw extension element capture for large models to improve performance.
    /// Only applies when OptimizeLargeModels=true and threshold is exceeded. Default false (zero-break).
    /// </summary>
    public bool SkipExtensionsForLargeModels { get; set; } = false;

    // Phase 7: Event Definition Enrichment (NEW)
    /// <summary>
    /// Enable normalization of event definitions into strongly-typed objects.
    /// When disabled, event definitions remain as raw XML in RawEventDefinitions only.
    /// Default true for Phase 7.
    /// </summary>
    public bool NormalizeEventDefinitions { get; set; } = true;

    /// <summary>
    /// Capture raw event definition XML elements for vendor/unknown definitions.
    /// Essential for maintaining roundtrip fidelity. Default true.
    /// </summary>
    public bool CaptureRawEventDefinitions { get; set; } = true;

    /// <summary>
    /// Generate diagnostics for unknown/vendor event definitions when encountered.
    /// Helps identify non-standard extensions that rely on raw preservation.
    /// Default true when EnableAdvancedValidation is true.
    /// </summary>
    public bool ValidateEventDefinitions { get; set; } = true;

    // Phase 7: Runtime semantic validation toggle
    /// <summary>
    /// Enable semantic validation of runtime constraints (e.g., event definition compatibility).
    /// Default true when EnableAdvancedValidation is true.
    /// </summary>
    public bool ValidateRuntimeSemantics { get; set; } = true;

    public bool UsePooledCollections { get; set; }
    public bool EnableNormalizedProjectionSerializer { get; set; } = false;
    public bool UseProjectionSerializer { get; set; } = false;
    public bool EnableCanonicalSort { get; set; } = false;
    /// <summary>
    /// Security configuration for parsing operations.
    /// </summary>
    public BpmnSecurityOptions SecurityOptions { get; init; } = new();

    /// <summary>
    /// Enable comprehensive security validation.
    /// </summary>
    public bool EnableSecurityValidation { get; init; } = true;

    /// <summary>
    /// Automatically sanitize detected security threats.
    /// </summary>
    public bool AutoSanitizeThreats { get; init; } = false;

    /// <summary>
    /// Fail parsing on any detected security threat.
    /// </summary>
    public bool FailOnSecurityThreat { get; init; } = true;
    /// <summary>
    /// Pluggable vendor extension handlers for custom namespace processing.
    /// </summary>
    public IReadOnlyList<IBpmnVendorExtensionInterpreter> VendorExtensionHandlers { get; init; } = Array.Empty<IBpmnVendorExtensionInterpreter>();

    /// <summary>
    /// Enable streaming parse mode for large BPMN files.
    /// </summary>
    public bool EnableStreamingParse { get; init; } = false;

    /// <summary>
    /// Threshold file size (in bytes) to trigger streaming mode.
    /// </summary>
    public int StreamingThreshold { get; init; } = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Chunk size for streaming operations.
    /// </summary>
    public int StreamingChunkSize { get; init; } = 64 * 1024; // 64KB

    /// <summary>
    /// Policy-based redaction configuration.
    /// </summary>
    public BpmnRedactionPolicies? RedactionPolicies { get; init; }

}