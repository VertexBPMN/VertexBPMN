namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Binding contract versions for connector templates (low-code plan LC02 / ADR-0006).
/// The runtime must be able to tell a legacy flat text mapping from a versioned binding
/// and to reject ambiguous combinations instead of guessing their meaning.
/// </summary>
public static class ConnectorBindingVersion
{
	/// <summary>Legacy flat attributes (<c>vertex:connector.*</c>, textual <c>ioMapping</c>).</summary>
	public const int Legacy = 1;

	/// <summary>
	/// Versioned binding: runtime-config, inputs, outputs and credential references are
	/// represented separately and unambiguously. Full support lands in LC02/LC03.
	/// </summary>
	public const int Versioned = 2;

	/// <summary>The newest binding version a current build knows about.</summary>
	public const int Current = Versioned;

	/// <summary>
	/// True when <paramref name="version"/> is known and can be executed by this build.
	/// Unknown/higher versions must be rejected at load time, not guessed.
	/// </summary>
	public static bool IsSupported(int version) => version is >= Legacy and <= Current;
}
