using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine;

namespace VertexBPMN.Parsing.ShadowMode;

/// <summary>
/// Phase 9: Shadow mode facade that wraps the unified parser
/// to provide backward compatibility with legacy Engine parser API.
/// Issues deprecation warnings when used directly.
/// </summary>
public sealed class LegacyEngineParserFacade
{
    private readonly BpmnParser _unifiedParser;
    private readonly EngineMapper _engineMapper;
    private readonly ILogger<LegacyEngineParserFacade>? _logger;
    private static bool _deprecationWarningIssued = false;

    public LegacyEngineParserFacade(ILogger<LegacyEngineParserFacade>? logger = null)
    {
        var options = new BpmnParserOptions
        {
            // Enable runtime projection features for engine compatibility
            BuildRuntimeProjection = true,
            NormalizeVendorExtensions = true,
            EnableAdvancedValidation = true,
            ThrowOnFatalValidation = true // Engine mode traditionally throws on errors
        };
        
        _unifiedParser = new BpmnParser(options);
        _engineMapper = new EngineMapper();
        _logger = logger;
    }

    /// <summary>
    /// Parses BPMN XML for engine execution using the unified parser.
    /// Issues deprecation warning on first use.
    /// </summary>
    [Obsolete("LegacyEngineParserFacade is deprecated. Use unified BpmnParser with BuildRuntimeProjection=true instead.", false)]
    public async Task<EngineProcessDefinition> ParseForEngineAsync(string xml, CancellationToken cancellationToken = default)
    {
        IssueDeprecationWarningOnce();
        
        // Parse using unified parser
        var model = await _unifiedParser.ParseAsync(xml, cancellationToken);
        
        // Map to engine format
        var mappingResult = _engineMapper.Map(model.ProcessId, model);
        
        if (mappingResult.ProcessDefinition == null)
        {
            throw new InvalidOperationException($"Failed to map BPMN model to engine format. Diagnostics: {string.Join("; ", mappingResult.MappingDiagnostics)}");
        }
        
        return mappingResult.ProcessDefinition;
    }

    private void IssueDeprecationWarningOnce()
    {
        if (!_deprecationWarningIssued)
        {
            _deprecationWarningIssued = true;
            _logger?.LogWarning("DEPRECATION: LegacyEngineParserFacade is deprecated and will be removed in a future version. " +
                               "Migrate to the unified BpmnParser with BuildRuntimeProjection=true for engine use cases. " +
                               "See docs/Unified-Parser-Migration-Guide.md for migration instructions.");
        }
    }
}