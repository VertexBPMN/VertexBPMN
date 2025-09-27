using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VertexBPMN.Parsing;

namespace VertexBPMN.Parsing.Hardening;

/// <summary>
/// Phase 11: Memory profiling utility for BPMN parser operations.
/// Measures memory usage patterns to detect leaks and optimization opportunities.
/// </summary>
public sealed class BpmnMemoryProfiler
{
    /// <summary>
    /// Profiles memory usage during a single parse operation.
    /// </summary>
    public async Task<MemoryProfileSnapshot> ProfileParseOperationAsync(string xml, BpmnParserOptions options)
    {
        // Force GC before measurement for accurate baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        var initialAllocated = GC.GetAllocatedBytesForCurrentThread();
        
        var parser = new BpmnParser(options);
        var stopwatch = Stopwatch.StartNew();
        
        // Track peak memory during parsing
        var peakMemory = initialMemory;
        var memoryTracker = Task.Run(async () =>
        {
            while (stopwatch.IsRunning)
            {
                var currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > peakMemory)
                {
                    peakMemory = currentMemory;
                }
                await Task.Delay(10); // Sample every 10ms
            }
        });
        
        // Execute the parse operation
        var model = await parser.ParseAsync(xml);
        stopwatch.Stop();
        
        // Stop memory tracking
        await memoryTracker;
        
        // Force GC to measure retained memory
        var beforeGc = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers(); 
        GC.Collect();
        var afterGc = GC.GetTotalMemory(false);
        
        var finalAllocated = GC.GetAllocatedBytesForCurrentThread();
        
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

/// <summary>
/// Snapshot of memory usage during a parse operation.
/// </summary>
public sealed record MemoryProfileSnapshot
{
    public double InitialMemoryUsageMB { get; init; }
    public double PeakMemoryUsageMB { get; init; }
    public double FinalMemoryUsageMB { get; init; }
    public double RetainedMemoryMB { get; init; }
    public double TotalAllocatedMB { get; init; }
    public double GcCollectedMB { get; init; }
    public TimeSpan ParseDuration { get; init; }
    public double StringInterningEffectiveness { get; init; }
    public int ElementCount { get; init; }
}