using System.Diagnostics.Metrics;

namespace VertexBPMN.Infrastructure.Operational;

public sealed class RuntimeMetricsState
{
    private IReadOnlyDictionary<string, long> _values = new Dictionary<string, long>();

    public RuntimeMetricsState()
    {
        RuntimeTelemetry.Meter.CreateObservableGauge(
            "vertexbpmn.runtime.state",
            Observe,
            description: "Current durable VertexBPMN runtime state grouped by metric name.");
    }

    public void Update(IReadOnlyDictionary<string, long> values) =>
        Interlocked.Exchange(ref _values, values);

    private IEnumerable<Measurement<long>> Observe() =>
        Volatile.Read(ref _values).Select(entry =>
            new Measurement<long>(entry.Value, new KeyValuePair<string, object?>("metric", entry.Key)));
}
