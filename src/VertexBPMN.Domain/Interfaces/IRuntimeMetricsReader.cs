namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Reads operational runtime metrics from the durable stores.
/// </summary>
public interface IRuntimeMetricsReader
{
    ValueTask<IReadOnlyDictionary<string, long>> ReadAsync(
        CancellationToken cancellationToken = default);
}
