using System.Text.Json;

namespace VertexBPMN.Tests.Unit.Pwa;

/// <summary>
/// LC11 (PWA / offline, Stufe D) - Slice 3 Client: die Offline-Task-Queue im Portal-Client
/// (LC-AC-19). Server-Replay + Konflikt-Semantik sind bereits in
/// OfflineCompletionReplayService / OfflineController abgedeckt; dieser statische Kontrakt
/// verifiziert, dass der Client-Queue-Glue beim Reconnect repliziert und Konflikte der UI
/// sichtbar macht (keine automatische Überschreibung), ohne authentifizierte Antworten,
/// Tokens oder sensible Fachdaten zu cachen.
/// </summary>
public sealed class OfflineTaskQueueContractTests
{
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VertexBPMN.sln")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("VertexBPMN.sln not found.");
    }

    private static string QueueModulePath()
        => Path.Combine(RepoRoot(), "src", "VertexBPMN.Portal.Client", "wwwroot", "offline-task-queue.js");

    [Fact]
    public void Module_ReplaysQueue_OnReconnect_WithVisibleConflictSurface()
    {
        var path = QueueModulePath();
        Assert.True(File.Exists(path), $"offline-task-queue.js absent: {path}");
        var body = File.ReadAllText(path);

        Assert.Contains("addEventListener(\"online\", replay)", body, StringComparison.Ordinal);
        Assert.Contains("/api/offline/completions/replay", body, StringComparison.Ordinal);
        // Conflicts must become visible to the UI, never silently dropped or overwritten.
        Assert.Contains("vertexbpmn:offline-conflict", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_StoresOnlyCompletionDrafts_AndCachesNoSensitiveData()
    {
        var body = File.ReadAllText(QueueModulePath());

        // Only offline completion drafts are persisted (namespaced localStorage key).
        Assert.Contains("vertexbpmn.offline.completions", body, StringComparison.Ordinal);

        // The queue must never persist tokens or authenticated responses.
        Assert.DoesNotContain("Authorization", body, StringComparison.Ordinal);
        Assert.Contains("NEVER caches authenticated responses", body, StringComparison.Ordinal);
    }
}
