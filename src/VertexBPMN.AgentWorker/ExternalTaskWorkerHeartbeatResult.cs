using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal sealed record ExternalTaskWorkerHeartbeatResult(
    ExternalTaskHeartbeatOutcome Outcome,
    long? LeaseExpiresAt = null);
