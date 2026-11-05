using System;

namespace VertexBPMN.Portal.Client.Bff;

/// <summary>Client-side status vocabulary, mirroring the BFF's stable server projection.</summary>
public static class TaskStatusProjector
{
    /// <summary>
    /// Normalizes a raw task lifecycle status into the portal's stable vocabulary:
    /// pending / active / completed / cancelled / unknown. Never surfaces raw strings.
    /// </summary>
    public static string Normalize(string? raw)
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
}
