namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>A complete Git object ID; no branch names, revision expressions or abbreviated IDs.</summary>
public sealed record GitCommitId
{
    public string Value { get; }

    public GitCommitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length is not (40 or 64) || value.Any(character => !Uri.IsHexDigit(character)))
		{
			throw new ArgumentException("A complete SHA-1 or SHA-256 Git object ID is required.", nameof(value));
		}

		Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
