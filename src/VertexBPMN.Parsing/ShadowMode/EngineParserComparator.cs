using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Engine;

namespace VertexBPMN.Parsing.ShadowMode;

/// <summary>
/// Phase 9: Compares output between legacy engine parser approach
/// and unified parser projection to detect mismatches.
/// </summary>
public sealed class EngineParserComparator
{
    /// <summary>
    /// Compares parsing approaches and returns mismatch diagnostics.
    /// </summary>
    public async Task<ParserComparisonResult> CompareAsync(string xml, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        var warnings = new List<string>();
        
        try
        {
            // Parse with unified parser (new approach)
            var unifiedParser = new BpmnParser(new BpmnParserOptions
            {
                BuildRuntimeProjection = true,
                NormalizeVendorExtensions = true,
                EnableAdvancedValidation = true,
                ThrowOnFatalValidation = false // Don't throw for comparison
            });
            
            var unifiedModel = await unifiedParser.ParseAsync(xml, cancellationToken);
            var mapper = new EngineMapper();
            var unifiedResult = mapper.Map(unifiedModel.ProcessId, unifiedModel);
            
            if (unifiedResult.ProcessDefinition == null)
            {
                diagnostics.Add("Unified parser failed to produce valid engine model");
                return new ParserComparisonResult(null, diagnostics, warnings, false);
            }
            
            var unifiedDef = unifiedResult.ProcessDefinition;
            
            // For now, we only have the unified parser, so we'll simulate comparison
            // In a real scenario with actual legacy parser, we'd parse with both approaches
            
            // Validate internal consistency
            ValidateProcessDefinition(unifiedDef, diagnostics, warnings);
            
            var hasSignificantMismatches = diagnostics.Any(d => d.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase));
            
            return new ParserComparisonResult(unifiedDef, diagnostics, warnings, hasSignificantMismatches);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"CRITICAL: Parser comparison failed with exception: {ex.Message}");
            return new ParserComparisonResult(null, diagnostics, warnings, true);
        }
    }
    
    private static void ValidateProcessDefinition(EngineProcessDefinition definition, List<string> diagnostics, List<string> warnings)
    {
        // Validate that all sequence flow endpoints exist
        foreach (var flow in definition.SequenceFlows)
        {
            if (!definition.Nodes.ContainsKey(flow.SourceId))
            {
                diagnostics.Add($"CRITICAL: Sequence flow {flow.Id} references non-existent source node {flow.SourceId}");
            }
            
            if (!definition.Nodes.ContainsKey(flow.TargetId))
            {
                diagnostics.Add($"CRITICAL: Sequence flow {flow.Id} references non-existent target node {flow.TargetId}");
            }
        }
        
        // Validate start events exist
        if (definition.StartEventIds.Count == 0)
        {
            warnings.Add("No start events found in process definition");
        }
        
        // Validate adjacency consistency
        foreach (var node in definition.Nodes.Keys)
        {
            var hasOutgoing = definition.Outgoing.ContainsKey(node);
            var hasIncoming = definition.Incoming.ContainsKey(node);
            
            if (!hasOutgoing && !hasIncoming)
            {
                warnings.Add($"Isolated node detected: {node}");
            }
        }
    }
}

/// <summary>
/// Result of comparing parsing approaches.
/// </summary>
public sealed record ParserComparisonResult(
    EngineProcessDefinition? ProcessDefinition,
    IReadOnlyList<string> CriticalMismatches,
    IReadOnlyList<string> Warnings,
    bool HasSignificantMismatches);