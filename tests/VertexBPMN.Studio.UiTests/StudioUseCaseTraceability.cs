using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Diagnostic provenance, never a substitute for the xUnit result. Uses the catalog
/// copied by the build, so editing the source catalog cannot relabel an old binary.
/// </summary>
internal sealed class StudioUseCaseTraceability
{
    private readonly string[] _catalogLines;
    private readonly string _revision;
    private readonly bool _dirty;
    private readonly IReadOnlyDictionary<string, string?> _assemblyHashes;
    private readonly string _catalogHash;

    private StudioUseCaseTraceability(string[] catalogLines, string revision, bool dirty,
        IReadOnlyDictionary<string, string?> assemblyHashes, string catalogHash)
    {
        _catalogLines = catalogLines;
        _revision = revision;
        _dirty = dirty;
        _assemblyHashes = assemblyHashes;
        _catalogHash = catalogHash;
    }

    public static StudioUseCaseTraceability Load(string repositoryRoot, string configuration)
    {
        var catalog = Path.Combine(AppContext.BaseDirectory, "studio-use-cases.tsv");
        var lines = File.ReadAllLines(catalog);
        var hashes = new Dictionary<string, string?>
        {
            ["tests"] = HashFile(typeof(StudioUseCaseTraceability).Assembly.Location),
            ["studio"] = HashFile(Path.Combine(repositoryRoot, "src/VertexBPMN.Studio/bin", configuration, "net10.0/VertexBPMN.Studio.dll")),
            ["api"] = HashFile(Path.Combine(repositoryRoot, "src/VertexBPMN.Api/bin", configuration, "net10.0/VertexBPMN.Api.dll"))
        };
        return new(lines, ReadGit(repositoryRoot, "rev-parse", "HEAD"),
            !string.IsNullOrWhiteSpace(ReadGit(repositoryRoot, "status", "--porcelain")), hashes, HashFile(catalog)!);
    }

    public string Describe(string runId, string scenario, string sessionId, string? testDisplayName)
    {
        var parts = scenario.Split("--", 2, StringSplitOptions.None);
        var method = parts[0];
        var variant = parts.Length == 2 ? parts[1] : null;
        var matches = _catalogLines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split('\t'))
            .Where(fields => fields.Length == 9 && fields[6] == method
                && (fields[7].Length == 0 || fields[7] == variant))
            .Select(fields => fields[0]).Order().ToArray();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1, runId, sessionId, method, variant, testDisplayName, useCaseIds = matches,
            revision = _revision, dirtyCheckout = _dirty, catalogSha256 = _catalogHash,
            assemblySha256 = _assemblyHashes, collectedAtUtc = DateTimeOffset.UtcNow,
            diagnosticOnly = true,
            resultSource = "results.xml (xUnit); presence of this file does not imply a passed test"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? HashFile(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ReadGit(string root, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process is null) return "unknown";
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000)) { process.Kill(); return "unknown"; }
            _ = error.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().Trim() : "unknown";
        }
        catch (System.ComponentModel.Win32Exception) { return "unknown"; }
    }
}
