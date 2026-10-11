using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal sealed record ExternalTaskWorkerMutationResult(ExternalTaskMutationOutcome Outcome);
