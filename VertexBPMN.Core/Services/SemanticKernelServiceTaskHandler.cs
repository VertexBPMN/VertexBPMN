using Microsoft.SemanticKernel;
using Polly;
using Polly.Retry;

namespace VertexBPMN.Core.Services;

public class SemanticKernelServiceTaskHandler: IServiceTaskHandler
{
    private readonly IKernelFactory _kernelFactory;
    private readonly AsyncRetryPolicy _retryPolicy;

    public SemanticKernelServiceTaskHandler(IKernelFactory kernelFactory)
    {
        _kernelFactory = kernelFactory;

        // Polly-Policy wird nur einmal im Konstruktor erstellt
        _retryPolicy = Policy
            .Handle<Exception>() // Besser: Handle<KernelException>() oder spezifischere Exceptions
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponentielles Backoff
    }

    public async Task ExecuteAsync(
        IDictionary<string, string> taskAttributes,
        IDictionary<string, object> processVariables,
        CancellationToken cancellationToken = default)
    {
        // 1. Attribute auslesen
        var resultVariable = GetAttribute(taskAttributes, "resultVariable", "llmResult");
        var promptSource = GetAttribute(taskAttributes, "prompt", null);

        // 2. Prompttext aus Prozessvariablen holen
        string promptText = processVariables.TryGetValue(promptSource ?? "", out var promptVar)
            ? promptVar?.ToString() ?? ""
            : promptSource ?? "";

        // 3. Kernel von der Factory holen (entweder neu erstellt oder aus dem Cache)
        var kernel = _kernelFactory.GetKernel(taskAttributes);
        var promptFunction = kernel.CreateFunctionFromPrompt(promptText);

        // 4. LLM-Call mit Retry-Policy ausführen
        var result = await _retryPolicy.ExecuteAsync(ct =>
                kernel.InvokeAsync(promptFunction, cancellationToken: ct),
            cancellationToken
        );

        // 5. Ergebnis in Prozessvariable schreiben
        processVariables[resultVariable] = result.GetValue<string>() ?? "";

    }

    private static string GetAttribute(IDictionary<string, string> attrs, string key, string defaultValue) =>
        attrs.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : defaultValue;
}