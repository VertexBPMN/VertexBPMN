using System.Diagnostics;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Security;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Memory profiling utility for BPMN parser operations.
/// Measures memory usage patterns to detect leaks and optimization opportunities.
/// </summary>
public sealed class BpmnMemoryProfiler
{
    /// <summary>
    /// Profiles process-wide managed memory during a parse operation.
    /// Run in a quiescent process: concurrent work cannot be attributed to this parser.
    /// </summary>
    public async Task<MemoryProfileSnapshot> ProfileParseOperationAsync(string xml, BpmnParserOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        // Force GC before measurement for accurate baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        var initialAllocated = GC.GetTotalAllocatedBytes(precise: true);
        
        var parser = new BpmnParser(options);
        var stopwatch = Stopwatch.StartNew();
        
        // Track peak memory during parsing
        var peakMemory = initialMemory;
        using var trackingCancellation = new CancellationTokenSource();
        var memoryTracker = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    peakMemory = Math.Max(peakMemory, GC.GetTotalMemory(false));
                    await Task.Delay(10, trackingCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (trackingCancellation.IsCancellationRequested) { }
        });
        
        // Execute the parse operation
        BpmnModel model;
        long finalAllocated;
        try
        {
            model = await parser.ParseAsync(xml, cancellationToken).ConfigureAwait(false);
            finalAllocated = GC.GetTotalAllocatedBytes(precise: true);
        }
        finally
        {
            stopwatch.Stop();
            trackingCancellation.Cancel();
            // Always join the tracker, including parser errors and cancellation.
            await memoryTracker.ConfigureAwait(false);
        }
        
        // Force GC to measure retained memory
        var beforeGc = GC.GetTotalMemory(false);
        peakMemory = Math.Max(peakMemory, beforeGc);
        GC.Collect();
        GC.WaitForPendingFinalizers(); 
        GC.Collect();
        var afterGc = GC.GetTotalMemory(false);
        
        peakMemory = Math.Max(peakMemory, afterGc);
        GC.KeepAlive(model);
        
        // Calculate string interning effectiveness
        var interningEffectiveness = CalculateStringInterningEffectiveness(model, options);
        
        return new MemoryProfileSnapshot
        {
            InitialMemoryUsageMB = initialMemory / (1024.0 * 1024.0),
            PeakMemoryUsageMB = peakMemory / (1024.0 * 1024.0),
            FinalMemoryUsageMB = afterGc / (1024.0 * 1024.0),
            RetainedMemoryMB = (afterGc - initialMemory) / (1024.0 * 1024.0),
            TotalAllocatedMB = (finalAllocated - initialAllocated) / (1024.0 * 1024.0),
            GcCollectedMB = (beforeGc - afterGc) / (1024.0 * 1024.0),
            ParseDuration = stopwatch.Elapsed,
            StringInterningEffectiveness = interningEffectiveness,
            ElementCount = CountModelElements(model)
        };
    }
    
    private static double CalculateStringInterningEffectiveness(Domain.Model.Bpmn.BpmnModel model, BpmnParserOptions options)
    {
        if (!options.InternIds)
            return 0.0;
        
        // Estimate interning effectiveness by counting unique vs total string references
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        var totalIdReferences = 0;
        
        // Count IDs across all model elements
        foreach (var evt in model.Events)
        {
            if (!string.IsNullOrEmpty(evt.Id))
            {
                uniqueIds.Add(evt.Id);
                totalIdReferences++;
            }
        }
        
        foreach (var task in model.Tasks)
        {
            if (!string.IsNullOrEmpty(task.Id))
            {
                uniqueIds.Add(task.Id);
                totalIdReferences++;
            }
        }
        
        foreach (var gw in model.Gateways)
        {
            if (!string.IsNullOrEmpty(gw.Id))
            {
                uniqueIds.Add(gw.Id);
                totalIdReferences++;
            }
        }
        
        foreach (var flow in model.SequenceFlows)
        {
            if (!string.IsNullOrEmpty(flow.Id))
            {
                uniqueIds.Add(flow.Id);
                totalIdReferences++;
            }
            if (!string.IsNullOrEmpty(flow.SourceRef))
            {
                uniqueIds.Add(flow.SourceRef);
                totalIdReferences++;
            }
            if (!string.IsNullOrEmpty(flow.TargetRef))
            {
                uniqueIds.Add(flow.TargetRef);
                totalIdReferences++;
            }
        }
        
        if (totalIdReferences == 0)
            return 0.0;
            
        // Effectiveness = (redundant references) / (total references)
        var redundantReferences = totalIdReferences - uniqueIds.Count;
        return (double)redundantReferences / totalIdReferences;
    }
    
    private static int CountModelElements(Domain.Model.Bpmn.BpmnModel model)
    {
        return model.Events.Count + 
               model.Tasks.Count + 
               model.Gateways.Count + 
               model.SequenceFlows.Count + 
               model.Subprocesses.Count;
    }
}
