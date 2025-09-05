#nullable enable
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Services;

/// <summary>
/// Fake implementation of <see cref="IAiDecisionService"/> for tests and in-memory scenarios.
/// Provides clone counting and optional clone callback.
/// </summary>
public sealed class FakeAiDecisionService : IAiDecisionService
{
    /*
    PSEUDOCODE PLAN
    - Maintain an int field _cloneInvocations for how many times Clone() was called on this instance.
    - Optional Action<FakeAiDecisionService>? _onClone invoked when a clone is created (for test hooks).
    - Constructors:
        - Public parameterless
        - Internal private that accepts callback and current invocation count (used during cloning)
    - Clone():
        - Increment _cloneInvocations thread-safely (Interlocked.Increment).
        - Create new FakeAiDecisionService passing same callback, with cloned count = 0 (fresh instance).
        - Invoke callback (if any) with the new instance.
        - Return the new instance as IAiDecisionService.
    - Expose CloneInvocations property (int) using Volatile.Read.
    - Provide static Create(factory) overload to allow specifying callback.
    - Ensure thread safety for counters.
    */

    private int _cloneInvocations;
    private readonly Action<FakeAiDecisionService>? _onClone;

    /// <summary>
    /// Number of times <see cref="Clone"/> has been invoked on this instance.
    /// </summary>
    public int CloneInvocations => Volatile.Read(ref _cloneInvocations);

    /// <summary>
    /// Creates a new fake without callbacks.
    /// </summary>
    public FakeAiDecisionService() : this(null) { }

    /// <summary>
    /// Factory method allowing an optional clone callback.
    /// </summary>
    /// <param name="onClone">Callback invoked with each newly created clone.</param>
    public static FakeAiDecisionService Create(Action<FakeAiDecisionService>? onClone = null) =>
        new(onClone);

    private FakeAiDecisionService(Action<FakeAiDecisionService>? onClone)
    {
        _onClone = onClone;
    }

    /// <inheritdoc />
    public IAiDecisionService Clone()
    {
        Interlocked.Increment(ref _cloneInvocations);
        var cloned = new FakeAiDecisionService(_onClone);
        _onClone?.Invoke(cloned);
        return cloned;
    }

    /// <summary>
    /// Resets the invocation counter (test utility).
    /// </summary>
    public void Reset() => Interlocked.Exchange(ref _cloneInvocations, 0);

    public Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default)
    {
        return null;
    }

    public Task<List<PlanItem>> PredictOptimalPlanItemsAsync(string caseId, Dictionary<string, object> caseFile, List<HistoricalCaseData> historicalData,
        CancellationToken cancellationToken = default)
    {
        return null;
    }
}