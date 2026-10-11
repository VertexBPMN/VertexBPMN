using System.Collections.Concurrent;
using System.Diagnostics;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Phase 11: Stress testing harness for concurrent BPMN parser operations.
/// Tests high-concurrency scenarios to detect deadlocks, memory leaks, and performance issues.
/// </summary>
public sealed class BpmnStressTester
{
	/// <summary>
	/// Executes parallel parse operations under stress conditions.
	/// </summary>
	public async Task<StressTestResult> ExecuteParallelParseTestAsync(string xml, int concurrentOperations, int totalOperations, TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(xml);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrentOperations);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalOperations);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Ticks);
		var result = new StressTestResult
		{
			TotalAttempted = totalOperations
		};
		using var cancellation = new CancellationTokenSource(timeout);
		var parser = new BpmnParser(new BpmnParserOptions
		{
			RoundtripMode = BpmnRoundtripMode.Normalized, // The stress test measures parse concurrency, not cache retention. Keeping cached
														  // models would intentionally retain memory and turn the leak detector into a false positive.
			CacheSize = 0,
			InternIds = true,
			EnableAdvancedValidation = true
		});
		// Warm up parser/JIT/static lookup tables before taking the retained-memory
		// baseline. Otherwise their one-time initialization is incorrectly reported
		// as a leak of the 10k-operation workload, especially on fresh CI runners.
		_ = await parser.ParseAsync(xml, cancellation.Token);
		var initialMemory = MeasureRetainedManagedMemory();
		var workload = new ParallelParseWorkload(parser, xml, concurrentOperations, cancellation.Token);
		var overallStopwatch = Stopwatch.StartNew();
		var midpoint = totalOperations / 2;
		await workload.ExecuteRangeAsync(0, midpoint);
		var midpointMemory = MeasureRetainedManagedMemory();
		await workload.ExecuteRangeAsync(midpoint, totalOperations - midpoint);
		overallStopwatch.Stop();
		result.CompletedSuccessfully = workload.CompletedSuccessfully;
		var finalMemory = MeasureRetainedManagedMemory();
		// Calculate results
		if (!workload.CompletionTimes.IsEmpty)
		{
			result.AverageParseTime = TimeSpan.FromTicks((long)workload.CompletionTimes.Average(t => t.Ticks));
		}

		result.ThroughputPerSecond = result.CompletedSuccessfully / overallStopwatch.Elapsed.TotalSeconds;
		// A leak is sustained retained growth, not one-time JIT, thread-pool or parser
		// initialization. Compare two equal workload phases after the first 5k parses.
		var retainedGrowth = finalMemory - midpointMemory;
		if (retainedGrowth > 100 * 1024 * 1024) // 100MB retained-growth threshold
		{
			result.MemoryLeakSuspects = 1;
		}

		// Detect potential deadlocks (operations that never completed)
		var timedOutOperations = totalOperations - result.CompletedSuccessfully - workload.Errors.Count;
		if (timedOutOperations > totalOperations * 0.1) // More than 10% timed out
		{
			result.DeadlockCount = timedOutOperations;
		}

		result.ErrorSample = [.. workload.Errors.Take(10)];
		result.TotalExecutionTime = overallStopwatch.Elapsed;
		result.MemoryUsedMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
		result.RetainedMemoryGrowthMB = retainedGrowth / (1024.0 * 1024.0);
		return result;
	}

	private sealed class ParallelParseWorkload(BpmnParser parser, string xml, int concurrentOperations, CancellationToken cancellationToken)
	{
		public ConcurrentBag<TimeSpan> CompletionTimes { get; } = [];
		public ConcurrentBag<string> Errors { get; } = [];

		public int CompletedSuccessfully;
		public async Task ExecuteRangeAsync(int start, int count)
		{
			var end = start + count;
			for (var batchStart = start; batchStart < end; batchStart += concurrentOperations)
			{
				var batchSize = Math.Min(concurrentOperations, end - batchStart);
				var batch = new Task[batchSize];
				for (var offset = 0; offset < batchSize; offset++)
				{
					var operationIndex = batchStart + offset;
					batch[offset] = Task.Run(() => ExecuteOperationAsync(operationIndex), cancellationToken);
				}

				try
				{
					await Task.WhenAll(batch);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
			}
		}

		private async Task ExecuteOperationAsync(int operationIndex)
		{
			var operationStopwatch = Stopwatch.StartNew();
			try
			{
				var model = await parser.ParseAsync(xml, cancellationToken);
				operationStopwatch.Stop();
				if (string.IsNullOrEmpty(model.ProcessId))
				{
					Errors.Add($"Operation {operationIndex}: Invalid model returned");
					return;
				}

				CompletionTimes.Add(operationStopwatch.Elapsed);
				Interlocked.Increment(ref CompletedSuccessfully);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Expected during cancellation.
			}
			catch (Exception ex)
			{
				Errors.Add($"Operation {operationIndex}: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S1215", Justification = "Explicit stress-test retained-memory measurements require full collection; this harness does not run during normal process execution.")]
	private static long MeasureRetainedManagedMemory()
	{
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
		return GC.GetTotalMemory(forceFullCollection: true);
	}
}
