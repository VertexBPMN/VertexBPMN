using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Configuration;

public sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string? GetSecret(string key, params string[] fallbackEnvironmentVariables)
    {
        var configuredValue = configuration[key];
        if (!string.IsNullOrWhiteSpace(configuredValue))
            return configuredValue;

        foreach (var environmentVariable in fallbackEnvironmentVariables)
        {
            var environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(environmentValue))
                return environmentValue;
        }

        return null;
    }
}