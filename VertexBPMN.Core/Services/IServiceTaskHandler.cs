namespace VertexBPMN.Core.Services;

/// <summary>
/// Handler f�r ServiceTasks. Implementierungen f�hren die eigentliche Business- oder KI-Logik aus.
/// </summary>
public interface IServiceTaskHandler
{
    /// <summary>
    /// F�hrt einen ServiceTask asynchron aus.
    /// attributes: BPMN-Attribute/extensionProperties (string->string)
    /// variables: Prozessvariablen (string->object), Implementierung darf diese modifizieren
    /// </summary>
    Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default);
}