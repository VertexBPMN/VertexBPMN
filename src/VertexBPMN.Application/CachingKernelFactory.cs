using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public class CachingKernelFactory : IKernelFactory
{
    private readonly ConcurrentDictionary<string, Kernel> _kernelCache = new();

    public Kernel GetKernel(IDictionary<string, string> attributes)
    {
        var provider = GetAttribute(attributes, "provider", "OpenAI");
        var modelId = GetAttribute(attributes, "modelId", "gpt-3.5-turbo");
        var cacheKey = $"{provider}-{modelId}";
        return _kernelCache.GetOrAdd(cacheKey, _ => CreateKernel(provider, modelId, attributes));
    }

    private static Kernel CreateKernel(
        string provider,
        string modelId,
        IDictionary<string, string> attributes)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var endpoint = GetOptionalAttribute(attributes, "endpoint");

        switch (provider.ToLowerInvariant())
        {
            case "openai":
                var openAiKey = GetEnvOrAttribute("OPENAI_API_KEY", attributes, "apiKey");
                kernelBuilder.AddOpenAIChatCompletion(modelId, openAiKey);
                break;

            case "azureopenai":
                var azureKey = GetEnvOrAttribute("AZURE_OPENAI_API_KEY", attributes, "apiKey");
                var azureEndpoint = endpoint
                                    ?? GetEnvOrAttribute("AZURE_OPENAI_ENDPOINT", attributes, "endpoint");
                kernelBuilder.AddAzureOpenAIChatCompletion(modelId, azureEndpoint, azureKey);
                break;

            default:
                throw new NotSupportedException($"Provider '{provider}' is not supported.");
        }

        return kernelBuilder.Build();
    }

    private static string GetEnvOrAttribute(
        string environmentVariable,
        IDictionary<string, string> attributes,
        string attributeName)
    {
        var value = GetOptionalAttribute(attributes, attributeName)
                    ?? Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Configuration value '{attributeName}' or environment variable '{environmentVariable}' is required.");
    }

    private static string GetAttribute(
        IDictionary<string, string> attributes,
        string key,
        string defaultValue) => GetOptionalAttribute(attributes, key) ?? defaultValue;

    private static string? GetOptionalAttribute(IDictionary<string, string> attributes, string key) =>
        attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
