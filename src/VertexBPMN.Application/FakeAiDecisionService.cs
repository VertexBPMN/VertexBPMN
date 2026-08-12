#nullable enable

using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Application;

/// <summary>
/// Fake implementation of <see cref="IAiDecisionService"/> for tests and in-memory scenarios.
/// Provides clone counting and optional clone callback.
/// </summary>
public sealed class FakeAiDecisionService : IAiDecisionService
{

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

    public Task<Dictionary<string, object>> FetchExternalContextAsync(string caseId, string resourceId, CancellationToken cancellationToken = default)
    {
        return null;
    }

    public Task ExecuteMcpActionAsync(string caseId, string mcpServerUrl, string method, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
       return null;
    }
}