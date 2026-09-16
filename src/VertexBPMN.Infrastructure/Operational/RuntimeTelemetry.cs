using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace VertexBPMN.Infrastructure.Operational;

public static class RuntimeTelemetry
{
    public const string ActivitySourceName = "VertexBPMN.Runtime";
    public const string MeterName = "VertexBPMN.Runtime.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> OutboxPublished =
        Meter.CreateCounter<long>("vertexbpmn.outbox.published");
    public static readonly Counter<long> OutboxFailures =
        Meter.CreateCounter<long>("vertexbpmn.outbox.failures");
    public static readonly Histogram<double> OutboxPublishDuration =
        Meter.CreateHistogram<double>("vertexbpmn.outbox.publish.duration", "ms");
    public static readonly Histogram<double> ExternalTaskQueueAge =
        Meter.CreateHistogram<double>("vertexbpmn.external_task.queue.age", "ms");
    public static readonly Counter<long> ExternalTaskLeaseLosses =
        Meter.CreateCounter<long>("vertexbpmn.external_task.lease.losses");
    public static readonly Counter<long> ExternalTaskRetries =
        Meter.CreateCounter<long>("vertexbpmn.external_task.retries");
    public static readonly Histogram<double> ExternalTaskDuration =
        Meter.CreateHistogram<double>("vertexbpmn.external_task.duration", "ms");
    public static readonly Counter<long> ExternalTaskSchemaFailures =
        Meter.CreateCounter<long>("vertexbpmn.external_task.schema.failures");
}
