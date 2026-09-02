using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IResilienceService
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName);
    Task ExecuteAsync(Func<Task> operation, string operationName);
    ResilienceStatus GetCircuitBreakerStatus(string operationName);
}