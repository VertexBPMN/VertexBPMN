namespace VertexBPMN.Domain.Interfaces;

public interface ISecretProvider
{
    string? GetSecret(string key, params string[] fallbackEnvironmentVariables);
}