using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal enum ExternalTaskHandlerOutcome { Success, TechnicalFailure, BusinessError }
