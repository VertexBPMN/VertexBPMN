using System.Diagnostics.Metrics;

namespace VertexBPMN.AgentWorker;

/// <summary>
/// Operational measurements only. Model-reported token counts are deliberately not exported as billing data.
/// </summary>
internal static class AgentWorkerTelemetry
{
    public const string MeterName = "VertexBPMN.AgentWorker.Metrics";
    private static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> BudgetFailures = Meter.CreateCounter<long>("vertexbpmn.agent.budget.failures");
    public static readonly Counter<long> SchemaFailures = Meter.CreateCounter<long>("vertexbpmn.agent.schema.failures");
    public static readonly Histogram<double> ReviewDuration = Meter.CreateHistogram<double>("vertexbpmn.agent.review.duration", "ms");
}
