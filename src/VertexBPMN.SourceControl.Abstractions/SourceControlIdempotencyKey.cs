namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Stable operation key; storage must enforce uniqueness and bind it to the request digest.</summary>
public sealed record SourceControlIdempotencyKey
{
    public string Value { get; }

    public SourceControlIdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
		{
			throw new ArgumentException("An ASCII operation key of at most 128 characters is required.", nameof(value));
		}

		Value = value;
    }

    public override string ToString() => Value;
}
