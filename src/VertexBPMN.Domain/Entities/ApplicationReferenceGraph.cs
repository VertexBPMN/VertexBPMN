using System.Text.Json;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

public sealed record ApplicationReferenceGraph(IReadOnlyDictionary<string, IReadOnlyList<string>> Edges)
{
    public static ApplicationReferenceGraph Read(JsonElement pages)
    {
        var edges = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var page in pages.EnumerateArray())
        {
            var id = page.GetProperty("id").GetString()!;
            if (!edges.TryAdd(id, page.TryGetProperty("navigation", out var navigation) ? navigation.EnumerateArray().Select(x => x.GetString()!).ToArray() : []))
            { throw new ApplicationValidationException("Duplicate page."); }
        }
        return new(edges);
    }

    public void Validate()
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Edges.Keys) { Visit(node, done, active); }
    }

    private void Visit(string node, HashSet<string> done, HashSet<string> active)
    {
        if (active.Contains(node)) { throw new ApplicationValidationException("Reference cycle."); }
        if (done.Contains(node)) { return; }
        if (!Edges.TryGetValue(node, out var targets)) { throw new ApplicationValidationException("Unknown navigation target."); }
        active.Add(node);
        foreach (var target in targets) { Visit(target, done, active); }
        active.Remove(node);
        done.Add(node);
    }
}
