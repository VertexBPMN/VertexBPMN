using System.Text.Json;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// LC10 reference-graph + compatibility check run BEFORE prepare/activate. It walks the
/// application revision's references (DMN/CMMN/BPMN processes, form revisions, connector/flow
/// contracts, UI versions) and detects missing or foreign artifacts. Missing or foreign
/// artifacts PREVENT activation — the release stays <see cref="ApplicationReleaseState.Prepared"/>
/// with a clear diagnostics payload instead of silently deploying.
/// </summary>
public static class ReleaseReferenceGraphValidator
{
    /// <summary>Result of the compatibility poll; <see cref="Diagnostics"/> is empty when compatible.</summary>
    public sealed record CompatibilityResult(bool IsCompatible, IReadOnlyList<string> Diagnostics);

    public static CompatibilityResult Validate(string contentJson, Func<ReferenceProbe, bool> artifactExists, string tenantId)
    {
        var diagnostics = new List<string>();
        var probes = ExtractReferenceProbes(contentJson);

        foreach (var probe in probes)
        {
            if (!artifactExists(probe))
            {
                diagnostics.Add($"Missing artifact '{probe.Kind}' key='{probe.Key}' revision='{probe.Revision}' for tenant '{tenantId}'.");
            }
            else if (probe.TenantId is not null && probe.TenantId != tenantId)
            {
                diagnostics.Add($"Foreign artifact '{probe.Kind}' key='{probe.Key}' belongs to tenant '{probe.TenantId}' not '{tenantId}'.");
            }
        }

        return new CompatibilityResult(diagnostics.Count == 0, diagnostics);
    }

    public static IReadOnlyList<ReferenceProbe> ExtractReferenceProbes(string contentJson)
    {
        var probes = new List<ReferenceProbe>();
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;

        if (root.TryGetProperty("references", out var references) && references.ValueKind == JsonValueKind.Array)
        {
            foreach (var reference in references.EnumerateArray())
            {
                var kind = reference.TryGetProperty("type", out var t) ? t.GetString() : null;
                var key = reference.TryGetProperty("key", out var k) ? k.GetString() : null;
                var revision = reference.TryGetProperty("revision", out var rev) ? rev.GetInt32() : 1;
                var tenantId = reference.TryGetProperty("tenantId", out var tid) ? tid.GetString() : null;
                probes.Add(new ReferenceProbe(kind ?? "unknown", key ?? string.Empty, revision, tenantId));
            }
        }

        // UI versions (page/component versions) are explicit compatibility inputs.
        if (root.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
                {
                    foreach (var component in components.EnumerateArray())
                    {
                        if (component.TryGetProperty("version", out var version) && !string.IsNullOrWhiteSpace(version.GetString()))
                        {
                            var id = component.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                            probes.Add(new ReferenceProbe("ui-component", string.IsNullOrWhiteSpace(id) ? "component" : id, TryParseRevision(version.GetString()!), null));
                        }
                    }
                }
            }
        }

        return probes;
    }

    private static int TryParseRevision(string value) =>
        int.TryParse(value, out var parsed) ? parsed : 1;

    public sealed record ReferenceProbe(string Kind, string Key, int Revision, string? TenantId);
}
