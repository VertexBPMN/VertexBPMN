using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Engine.Parsing;

/// <summary>
/// Parses DMN XML into a DmnDecision model.
/// </summary>
public class DmnParser : IDmnParser
{
    private readonly ILogger<DmnParser> _logger;

    public DmnParser(ILogger<DmnParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses a DMN XML string into a DmnDecision model asynchronously.
    /// </summary>
    /// <param name="dmnXml">The DMN XML string to parse.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A DmnDecision instance.</returns>
    public async Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dmnXml))
            throw new DmnParseException("DMN XML cannot be null or empty");

        try
        {
            var doc = await Task.Run(() => XDocument.Parse(dmnXml), cancellationToken);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.Get("https://www.omg.org/spec/DMN/20191111/MODEL/");
            var decision = doc.Descendants(ns + "decision").FirstOrDefault()
                ?? throw new DmnParseException("No decision element found in DMN XML");

            var decisionId = decision.Attribute("id")?.Value ?? throw new DmnParseException("Decision ID missing");
            var decisionName = decision.Attribute("name")?.Value ?? decisionId;

            // Parse Hit Policy
            var decisionTable = decision.Element(ns + "decisionTable")
                ?? throw new DmnParseException("No decisionTable element found");
            var hitPolicy = decisionTable.Attribute("hitPolicy")?.Value?.ToUpper() ?? "UNIQUE";
            if (!new[] { "UNIQUE", "FIRST", "PRIORITY", "COLLECT" }.Contains(hitPolicy))
                throw new DmnParseException($"Unsupported hit policy: {hitPolicy}");

            // Parse Inputs (support both DMN forms: with <inputExpression> and direct attributes)
            var inputs = decisionTable.Descendants(ns + "input").Select(input =>
            {
                var inputId = input.Attribute("id")?.Value ?? throw new DmnParseException("Input ID missing");
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
            }).ToList();

            // Parse Outputs
            var outputs = decisionTable.Descendants(ns + "output").Select(output => new DmnOutput(
                output.Attribute("id")?.Value ?? throw new DmnParseException("Output ID missing"),
                output.Attribute("label")?.Value ?? output.Attribute("id")?.Value ?? "",
                output.Attribute("typeRef")?.Value ?? "string"
            )).ToList();

            // Parse Rules
            var rules = decisionTable.Descendants(ns + "rule").Select(rule =>
            {
                var ruleId = rule.Attribute("id")?.Value ?? throw new DmnParseException("Rule ID missing");
                var inputConditions = rule.Descendants(ns + "inputEntry").Select(entry =>
                {
                    var inputRef = entry.Attribute("id")?.Value ?? throw new DmnParseException($"InputEntry in rule {ruleId} missing ID");
                    return KeyValuePair.Create(inputRef, entry.Element(ns + "text")?.Value ?? "-");
                }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var outputValues = rule.Descendants(ns + "outputEntry").Select(entry =>
                {
                    var outputRef = entry.Attribute("id")?.Value ?? throw new DmnParseException($"OutputEntry in rule {ruleId} missing ID");
                    var value = entry.Element(ns + "text")?.Value ?? "";
                    return KeyValuePair.Create(outputRef, (object)value);
                }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                return new DmnRule(ruleId, inputConditions, outputValues);
            }).ToList();

            // Validate
            ValidateDecision(decisionId, inputs, outputs, rules, hitPolicy);

            var decisionModel = new DmnDecision(decisionId, decisionName, inputs, outputs, rules, hitPolicy);
            _logger.LogInformation("Parsed DMN decision {DecisionId} with hit policy {HitPolicy}", decisionId, hitPolicy);
            return decisionModel;
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

    private void ValidateDecision(string decisionId, List<DmnInput> inputs, List<DmnOutput> outputs, List<DmnRule> rules, string hitPolicy)
    {
        if (!inputs.Any())
            throw new DmnParseException($"Decision {decisionId} has no inputs defined");
        if (!outputs.Any())
            throw new DmnParseException($"Decision {decisionId} has no outputs defined");
        if (!rules.Any())
            throw new DmnParseException($"Decision {decisionId} has no rules defined");

        foreach (var rule in rules)
        {
            if (rule.InputConditions.Count != inputs.Count)
                throw new DmnParseException($"Rule {rule.Id} has mismatched input conditions count");
            if (rule.OutputValues.Count != outputs.Count)
                throw new DmnParseException($"Rule {rule.Id} has mismatched output values count");
        }

        if (hitPolicy == "PRIORITY" && !outputs.All(o => o.TypeRef is "integer" or "double" or "string"))
            throw new DmnParseException($"Decision {decisionId} with PRIORITY hit policy requires comparable output types");
    }
}
