using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

/// <summary>
/// Resolves a binding value against the LC01 configuration contract
/// (docs/reference/low-code-application-contracts.md): a source is either a literal,
/// a variable reference (<c>{{name}}</c>) or a configuration reference. Anything
/// ambiguous or unresolved fails fast instead of being silently reinterpreted.
/// </summary>
public static class ConnectorValueResolver
{
	private const string OpenDelimiter = "{{";
	private const string CloseDelimiter = "}}";

	/// <summary>
	/// Resolves a source against the engine variables. Returns the literal when the
	/// source has no variable-reference delimiters; a lone <c>{{name}}</c> is resolved
	/// against variables; mixed literal+reference content is assembled. Unresolved
	/// references and malformed delimiters throw <see cref="ServiceTaskExecutionException"/>.
	/// </summary>
	public static string Resolve(string source, IReadOnlyDictionary<string, object?> variables, string? sourceDescription = null)
	{
		if (string.IsNullOrEmpty(source))
		{
			return source;
		}

		var description = sourceDescription ?? source;
		if (!source.Contains(OpenDelimiter, StringComparison.Ordinal))
		{
			return source; // literal
		}

		var builder = new StringBuilder();
		var position = 0;
		while (true)
		{
			var open = source.IndexOf(OpenDelimiter, position, StringComparison.Ordinal);
			if (open < 0)
			{
				builder.Append(source, position, source.Length - position);
				break;
			}

			builder.Append(source, position, open - position);
			var close = source.IndexOf(CloseDelimiter, open + OpenDelimiter.Length, StringComparison.Ordinal);
			if (close < 0)
			{
				throw new ServiceTaskExecutionException($"Malformed variable reference in '{description}': missing '{CloseDelimiter}'.");
			}

			var name = source[(open + OpenDelimiter.Length)..close].Trim();
			if (name.Length == 0)
			{
				throw new ServiceTaskExecutionException($"Empty variable reference in '{description}'.");
			}

			if (!variables.TryGetValue(name, out var value))
			{
				throw new ServiceTaskExecutionException($"Variable '{name}' referenced by '{description}' is not available in the current context.");
			}

			builder.Append(Convert.ToString(value) ?? string.Empty);
			position = close + CloseDelimiter.Length;
		}

		return builder.ToString();
	}
}
