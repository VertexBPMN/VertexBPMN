using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal sealed record ExternalTaskHandlerResult(
    ExternalTaskHandlerOutcome Outcome,
    JsonElement Result = default,
    string? Code = null)
{
    public static ExternalTaskHandlerResult Success(JsonElement result) =>
        new(ExternalTaskHandlerOutcome.Success, result.Clone());
    public static ExternalTaskHandlerResult TechnicalFailure(string code) =>
        new(ExternalTaskHandlerOutcome.TechnicalFailure, Code: code);
    public static ExternalTaskHandlerResult BusinessError(string code) =>
        new(ExternalTaskHandlerOutcome.BusinessError, Code: code);
}
