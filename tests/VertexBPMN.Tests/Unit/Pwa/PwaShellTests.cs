using System.Text.Json;

namespace VertexBPMN.Tests.Unit.Pwa;

/// <summary>
/// LC11 (PWA / offline, Stufe D) - Slice 1: die installierbare Portal-Shell (LC-AC-18).
/// Statische UI-Assets sind der Standardcache; diese Verträge verifizieren die PWA-Installierbarkeit
/// (Web-App-Manifest + Service-Worker-Registrierung), nicht das Caching sensibler/Fachdaten.
/// </summary>
public sealed class PwaShellTests
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

    private static string ClientWwwRoot()
        => Path.Combine(RepoRoot(), "src", "VertexBPMN.Portal.Client", "wwwroot");

    [Fact]
    public void Manifest_IsAValidInstallablePwaShell()
    {
        var manifestPath = Path.Combine(ClientWwwRoot(), "manifest.webmanifest");
        Assert.True(File.Exists(manifestPath), $"manifest absent: {manifestPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("name").GetString()), "name required");
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("short_name").GetString()), "short_name required");
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() is "192x192"
                                    && i.GetProperty("type").GetString() == "image/png"
                                    && i.GetProperty("src").GetString() is not null);
    }

    [Fact]
    public void Index_Html_LinksTheManifestAndRegistersTheServiceWorker()
    {
        var index = File.ReadAllText(Path.Combine(ClientWwwRoot(), "index.html"));
        Assert.Contains("rel=\"manifest\" href=\"manifest.webmanifest\"", index, StringComparison.Ordinal);
        Assert.Contains("navigator.serviceWorker.register", index, StringComparison.Ordinal);
        Assert.Contains("service-worker.js", index, StringComparison.Ordinal);
    }
}
