using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal enum ExternalTaskMutationOutcome { Accepted, LeaseLost, Rejected }
