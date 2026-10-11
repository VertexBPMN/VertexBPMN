using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Engine.Parsing;

/// <summary>
/// Parses DMN XML into a DmnDecision model.
/// </summary>
public class DmnParser(ILogger<DmnParser> logger) : IDmnParser
{
	private readonly ILogger<DmnParser> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Parses a DMN XML string into a DmnDecision model asynchronously.
	/// </summary>
	/// <param name="dmnXml">The DMN XML string to parse.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A DmnDecision instance.</returns>
	public async Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(dmnXml))
		{
			throw new DmnParseException("DMN XML cannot be null or empty");
		}
		try
		{
			var doc = await Task.Run(() => XDocument.Parse(dmnXml), cancellationToken);
			var ns = doc.Root?.Name.Namespace ?? XNamespace.Get("https://www.omg.org/spec/DMN/20191111/MODEL/");
			var decision = doc.Descendants(ns + "decision").FirstOrDefault()
				?? throw new DmnParseException("No decision element found in DMN XML");

			var decisionId = decision.Attribute("id")?.Value ?? throw new DmnParseException("Decision ID missing");
			var decisionName = decision.Attribute("name")?.Value ?? decisionId;

			// Parse Hit Policy
			var decisionTable = decision.Element(ns + "decisionTable");
			if (decisionTable is null)
			{
				return ReadLiteralDecision(decision, ns, decisionId, decisionName, dmnXml);
			}
			return ReadDecisionTable(decisionTable, ns, decisionId, decisionName, dmnXml);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (XmlException ex)
		{
			_logger.LogError(ex, "Invalid DMN XML format at line {LineNumber}, position {LinePosition}: {Message}", ex.LineNumber, ex.LinePosition, ex.Message);
			throw new DmnParseException($"Invalid DMN XML format at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to parse DMN XML for decision");
			throw new DmnParseException("Failed to parse DMN XML", ex);
		}
	}

	private DmnDecision ReadLiteralDecision(XElement decision, XNamespace ns, string decisionId, string decisionName, string dmnXml)
	{
		if (decision.Element(ns + "literalExpression") is null)
		{
			throw new DmnParseException("Decision contains no supported boxed expression");
		}
		var literalModel = new DmnDecision(decisionId, decisionName, [], [], [],
			SourceXml: dmnXml, EvaluationTargetId: decisionId);
		if (_logger.IsEnabled(LogLevel.Information))
		{
			_logger.LogInformation("Parsed DMN literal decision {DecisionId}", decisionId);
		}
		return literalModel;
	}

	private DmnDecision ReadDecisionTable(XElement decisionTable, XNamespace ns, string decisionId, string decisionName, string dmnXml)
	{
		var hitPolicy = decisionTable.Attribute("hitPolicy")?.Value?.ToUpperInvariant() ?? "UNIQUE";
		if (hitPolicy is not ("UNIQUE" or "FIRST" or "PRIORITY" or "ANY" or "COLLECT" or "RULE ORDER" or "OUTPUT ORDER"))
		{
			throw new DmnParseException($"Unsupported hit policy: {hitPolicy}");
		}

		var inputs = ReadInputs(decisionTable, ns);
		var outputs = ReadOutputs(decisionTable, ns);
		var rules = ReadRules(decisionTable, ns, inputs, outputs);

		// Validate
		ValidateDecision(decisionId, inputs, outputs, rules, hitPolicy);

		var decisionModel = new DmnDecision(
			decisionId,
			decisionName,
			inputs,
			outputs,
			rules,
			hitPolicy,
			dmnXml,
			decisionId);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Parsed DMN decision {DecisionId} with hit policy {HitPolicy}", decisionId, hitPolicy);
		}
		return decisionModel;
	}


	private static List<DmnInput> ReadInputs(XElement decisionTable, XNamespace ns)
	{
		return [.. decisionTable.Elements(ns + "input").Select((input, index) =>
			{
				var inputId = input.Attribute("id")?.Value ?? $"input-{index + 1}";
				var inputExpression = input.Element(ns + "inputExpression");
                // Determine typeRef with fallbacks
                var typeRef = inputExpression?.Element(ns + "typeRef")?.Value
							  ?? inputExpression?.Attribute("typeRef")?.Value
							  ?? input.Attribute("typeRef")?.Value
							  ?? "string";
				return new DmnInput(
					inputId,
					input.Attribute("label")?.Value ?? inputId,
					typeRef
				);
			})];
	}

	private static List<DmnOutput> ReadOutputs(XElement decisionTable, XNamespace ns)
	{
		return [.. decisionTable.Elements(ns + "output").Select((output, index) => new DmnOutput(
				output.Attribute("id")?.Value ?? $"output-{index + 1}",
				output.Attribute("name")?.Value ?? output.Attribute("label")?.Value ?? output.Attribute("id")?.Value ?? $"output-{index + 1}",
				output.Attribute("typeRef")?.Value ?? "string"
			))];
	}

	private static List<DmnRule> ReadRules(XElement decisionTable, XNamespace ns, List<DmnInput> inputs, List<DmnOutput> outputs)
	{
		return [.. decisionTable.Elements(ns + "rule").Select((rule, ruleIndex) =>
			{
				var ruleId = rule.Attribute("id")?.Value ?? $"rule-{ruleIndex + 1}";
				var inputConditions = rule.Elements(ns + "inputEntry")
					.Select((entry, index) => KeyValuePair.Create(
						index < inputs.Count ? inputs[index].Id : $"input-{index + 1}",
						entry.Element(ns + "text")?.Value ?? entry.Value ?? "-"))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

				var outputValues = rule.Elements(ns + "outputEntry")
					.Select((entry, index) => KeyValuePair.Create(
						index < outputs.Count ? outputs[index].Id : $"output-{index + 1}",
						(object)(entry.Element(ns + "text")?.Value ?? entry.Value ?? "")))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

				return new DmnRule(ruleId, inputConditions, outputValues);
			})];
	}

	private static void ValidateDecision(string decisionId, List<DmnInput> inputs, List<DmnOutput> outputs, List<DmnRule> rules, string hitPolicy)
	{
		if (inputs.Count == 0)
		{
			throw new DmnParseException($"Decision {decisionId} has no inputs defined");
		}

		if (outputs.Count == 0)
		{
			throw new DmnParseException($"Decision {decisionId} has no outputs defined");
		}

		if (rules.Count == 0)
		{
			throw new DmnParseException($"Decision {decisionId} has no rules defined");
		}

		foreach (var rule in rules)
		{
			if (rule.InputConditions.Count != inputs.Count)
			{
				throw new DmnParseException($"Rule {rule.Id} has mismatched input conditions count");
			}

			if (rule.OutputValues.Count != outputs.Count)
			{
				throw new DmnParseException($"Rule {rule.Id} has mismatched output values count");
			}
		}

		if (hitPolicy == "PRIORITY" && !outputs.All(o => o.TypeRef is "integer" or "double" or "string"))
		{
			throw new DmnParseException($"Decision {decisionId} with PRIORITY hit policy requires comparable output types");
		}
	}
}
