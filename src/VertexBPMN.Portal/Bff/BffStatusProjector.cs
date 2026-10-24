using System.Text.Json;

namespace VertexBPMN.Portal.Bff;

/// <summary>
/// Pure, server-side projections for the portal BFF. These map released
/// application content and task lifecycle data into the DTOs served to the
/// portal client; the client never receives raw source or template data.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public projector invoked by the public BFF controller and runtime gateway.")]
public static class BffStatusProjector
{
    /// <summary>Normalizes a raw task lifecycle state to the portal's stable status vocabulary.</summary>
    public static string NormalizeTaskStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return "unknown"; }
        return raw.Trim().ToLowerInvariant() switch
        {
            "created" or "pending" or "ready" => "pending",
            "active" or "in_progress" or "claimed" or "assigned" or "running" => "active",
            "completed" or "done" or "finished" => "completed",
            "cancelled" or "canceled" or "withdrawn" => "cancelled",
            _ => "unknown",
        };
    }

    public static TaskStatusProjection ProjectTaskStatus(Guid taskId, string? rawStatus, string? candidateRole, DateTimeOffset? created, DateTimeOffset? completed)
        => new(taskId, NormalizeTaskStatus(rawStatus), candidateRole, created, completed);

    /// <summary>Builds the released application metadata the client may render.</summary>
    public static ReleasedApplicationDto ProjectReleased(ReleasedApplication application)
    {
        var routes = new List<string>();
        using var document = JsonDocument.Parse(application.ContentJson);
        if (document.RootElement.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("route", out var route) && route.ValueKind == JsonValueKind.String)
                {
                    routes.Add(route.GetString()!);
                }
            }
        }

        return new ReleasedApplicationDto(application.ApplicationId, application.Version, routes.Count, routes);
    }
}
