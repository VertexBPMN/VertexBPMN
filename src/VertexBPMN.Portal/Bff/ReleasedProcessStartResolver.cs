using System.Text.Json;

namespace VertexBPMN.Portal.Bff;

/// <summary>
/// Narrow, server-side gate for process start against the released revision. It only
/// resolves the processDefinitionId of a sanctioned <c>start-process</c> action that exists
/// verbatim in the released content; anything else is rejected as unsanctioned. The
/// resolved processDefinitionId is handed to the engine start, never caller-supplied.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public resolver invoked by the public BFF controller.")]
public static class ReleasedProcessStartResolver
{
    /// <summary>
    /// Resolves the processDefinitionId of the sanctioned start-process action in the released
    /// revision. Throws <see cref="BffUnsanctionedActionException"/> when no start-process action
    /// exists or it lacks a non-empty processDefinitionId. Returns the resolved process definition id.
    /// </summary>
    public static string ResolveSanctionedStartProcessId(string contentJson)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            throw new BffUnsanctionedActionException("The released application sanctions no process start.");
        }

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var action in actions.EnumerateArray())
            {
                if (!action.TryGetProperty("type", out var type) || type.GetString() != ReleasedActions.StartProcess)
                {
                    continue;
                }

                if (!action.TryGetProperty("processDefinitionId", out var processId)
                    || processId.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(processId.GetString()))
                {
                    throw new BffUnsanctionedActionException("The sanctioned start-process action references no processDefinitionId.");
                }

                return processId.GetString()!;
            }
        }

        throw new BffUnsanctionedActionException("The released application sanctions no process start.");
    }
}
