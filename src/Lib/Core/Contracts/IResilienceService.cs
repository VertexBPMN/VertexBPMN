using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts;

public interface IResilienceService
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName);
    Task ExecuteAsync(Func<Task> operation, string operationName);
    ResilienceStatus GetCircuitBreakerStatus(string operationName);
}