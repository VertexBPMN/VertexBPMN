using System.Collections.Concurrent;
using Microsoft.SemanticKernel;

namespace VertexBPMN.Core.Tasks;

public class CachingKernelFactory : IKernelFactory
{
    // Thread-sicherer Cache, um Kernel-Instanzen wiederzuverwenden
    private readonly ConcurrentDictionary<string, Kernel> _kernelCache = new();

    public Kernel GetKernel(IDictionary<string, string> attributes)
    {
        var provider = GetAttribute(attributes, "provider", "OpenAI");
        var modelId = GetAttribute(attributes, "modelId", "gpt-3.5-turbo");

        // Erzeuge einen einzigartigen Cache-Schlüssel für die Provider/Modell-Kombination
        string cacheKey = $"{provider}-{modelId}";

        // Gib den Kernel aus dem Cache zurück oder erstelle ihn neu, falls nicht vorhanden.
        return _kernelCache.GetOrAdd(cacheKey, _ => CreateKernel(provider, modelId, attributes));
    }

    private Kernel CreateKernel(string provider, string modelId, IDictionary<string, string> attributes)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var endpoint = GetAttribute(attributes, "endpoint", null);

        switch (provider.ToLowerInvariant())
        {
            case "openai":
                var openAiKey = GetEnvOrAttr("OPENAI_API_KEY", attributes, "apiKey");
                kernelBuilder.AddOpenAIChatCompletion(modelId, openAiKey);
                break;

            case "azureopenai":
                var azureKey = GetEnvOrAttr("AZURE_OPENAI_API_KEY", attributes, "apiKey");
                var azureEndpoint = endpoint ?? GetEnvOrAttr("AZURE_OPENAI_ENDPOINT", attributes, "endpoint");
                kernelBuilder.AddAzureOpenAIChatCompletion(modelId, azureEndpoint, azureKey);
                break;

            // ... weitere Provider ...

            default:
                throw new NotSupportedException($"Provider '{provider}' wird nicht unterstützt.");
        }

        return kernelBuilder.Build();
    }

    // Die Hilfsmethoden bleiben dieselben wie in deiner ursprünglichen Klasse
    private static string GetEnvOrAttr(string envName, IDictionary<string, string> attrs, string attrName) =>
        attrs.TryGetValue(attrName, out var val) && !string.IsNullOrWhiteSpace(val) ? val : Environment.GetEnvironmentVariable(envName);

    private static string GetAttribute(IDictionary<string, string> attrs, string key, string defaultValue) =>
        attrs.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : defaultValue;
}