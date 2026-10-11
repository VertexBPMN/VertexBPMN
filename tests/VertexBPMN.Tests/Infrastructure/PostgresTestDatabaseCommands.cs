using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Npgsql;

namespace VertexBPMN.Tests.Infrastructure;

/// <summary>Limits local acceptance-test administration to single safe database identifiers.</summary>
internal static class PostgresTestDatabaseCommands
{
	private static readonly Regex AllowedCommand = new(
		"\\A(?:CREATE DATABASE \"[a-z][a-z0-9_]{0,62}\"|DROP DATABASE(?: IF EXISTS)? \"[a-z][a-z0-9_]{0,62}\" WITH \\(FORCE\\))\\z",
		RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

	[SuppressMessage("Security", "CA2100", Justification = "The complete command is validated against a closed CREATE/DROP grammar and a bounded ASCII identifier allowlist. SQL identifiers cannot be parameterized.")]
	public static NpgsqlCommand CreateValidated(NpgsqlConnection connection, string commandText)
	{
		ArgumentNullException.ThrowIfNull(connection);
		Validate(commandText);
		return new NpgsqlCommand(commandText, connection);
	}

	internal static void Validate(string commandText)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
		if (!AllowedCommand.IsMatch(commandText))
		{
			throw new ArgumentException("Only isolated test-database CREATE/DROP commands are permitted.", nameof(commandText));
		}
	}
}
