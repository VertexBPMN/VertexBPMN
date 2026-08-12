namespace VertexBPMN.Domain.Entities.Modeling;

public record DmnRule(string Id, IReadOnlyDictionary<string, string> InputConditions, IReadOnlyDictionary<string, object> OutputValues)
{
    // Backward compatibility: old ctor (only input list & output list) without Id
    public DmnRule(IReadOnlyList<string> inputEntries, IReadOnlyList<string> outputEntries)
        : this($"rule_{System.Guid.NewGuid():N}",
            inputEntries.Select((v, i) => new { i, v })
                .ToDictionary(x => $"i{x.i + 1}", x => x.v ?? string.Empty),
            outputEntries.Select((v, i) => new { i, v })
                .ToDictionary(x => $"o{x.i + 1}", x => (object)(x.v ?? string.Empty))) { }

    // Backward compatibility: old ctor with explicit Id omitted, matching tests using List<string>, List<string>
    public DmnRule(List<string> inputEntries, List<string> outputEntries)
        : this((IReadOnlyList<string>)inputEntries, (IReadOnlyList<string>)outputEntries) { }
}