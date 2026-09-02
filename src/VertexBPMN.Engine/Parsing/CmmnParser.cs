using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Engine.Parsing;

public sealed class CmmnParser : ICmmnParser
{
    private static readonly HashSet<string> DefinitionTypes = new(StringComparer.Ordinal)
    {
        "humanTask", "manualTask", "serviceTask", "processTask", "caseTask",
        "decisionTask", "task", "stage", "milestone", "eventListener",
        "userEventListener", "timerEventListener"
    };

    public Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cmmnXml);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var textReader = new StringReader(cmmnXml);
            using var reader = XmlReader.Create(textReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = 10_000_000,
                IgnoreComments = false
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var caseElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "case")
                ?? throw new CmmnParseException("No case found in CMMN XML");
            var caseId = RequiredAttribute(caseElement, "id", "Case ID missing");
            var caseName = Attribute(caseElement, "name") ?? caseId;

            var definitions = caseElement.Descendants()
                .Where(element => DefinitionTypes.Contains(element.Name.LocalName) && Attribute(element, "id") is not null)
                .ToDictionary(element => Attribute(element, "id")!, StringComparer.Ordinal);
            var planItems = caseElement.Descendants()
                .Where(element => element.Name.LocalName is "planItem" or "discretionaryItem")
                .Select(element => ParsePlanItem(element, caseElement, definitions))
                .ToList();
            var sentries = caseElement.Descendants()
                .Where(element => element.Name.LocalName == "sentry")
                .Select(ParseSentry)
                .ToList();
            var caseFileItems = caseElement.Descendants()
                .Where(element => element.Name.LocalName == "caseFileItem")
                .Select(element => new CaseFileItem(
                    RequiredAttribute(element, "id", "Case file item ID missing"),
                    Attribute(element, "name") ?? Attribute(element, "id")!,
                    null!))
                .ToList();

            return Task.FromResult(new CaseModel(
                caseId, caseName, planItems, sentries, caseFileItems, Attributes(caseElement)));
        }
        catch (CmmnParseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or ArgumentException)
        {
            throw new CmmnParseException("Failed to parse CMMN XML", exception);
        }
    }

    private static PlanItem ParsePlanItem(
        XElement element,
        XElement caseElement,
        IReadOnlyDictionary<string, XElement> definitions)
    {
        var id = RequiredAttribute(element, "id", $"{element.Name.LocalName} ID missing");
        var definitionRef = Attribute(element, "definitionRef")
            ?? throw new CmmnParseException($"Plan item '{id}' has no definitionRef.");
        if (!definitions.TryGetValue(definitionRef, out var definition))
            throw new CmmnParseException($"Plan item '{id}' references unknown definition '{definitionRef}'.");

        var attributes = Attributes(definition);
        foreach (var attribute in Attributes(element)) attributes[attribute.Key] = attribute.Value;
        attributes["definitionName"] = Attribute(definition, "name") ?? definitionRef;

        var parentDefinition = element.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == "stage" && Attribute(ancestor, "id") is not null);
        var parentPlanItemId = parentDefinition is null
            ? null
            : caseElement.Descendants().FirstOrDefault(candidate =>
                candidate.Name.LocalName == "planItem"
                && Attribute(candidate, "definitionRef") == Attribute(parentDefinition, "id")) is { } parent
                ? Attribute(parent, "id")
                : null;

        return new PlanItem(
            id,
            definition.Name.LocalName,
            definitionRef,
            attributes,
            element.Elements().Where(child => child.Name.LocalName == "entryCriterion")
                .Select(child => Attribute(child, "sentryRef") ?? string.Empty)
                .Where(reference => reference.Length > 0).ToList(),
            element.Elements().Where(child => child.Name.LocalName == "exitCriterion")
                .Select(child => Attribute(child, "sentryRef") ?? string.Empty)
                .Where(reference => reference.Length > 0).ToList(),
            element.Name.LocalName == "discretionaryItem",
            parentPlanItemId);
    }

    private static Sentry ParseSentry(XElement element)
    {
        var id = RequiredAttribute(element, "id", "Sentry ID missing");
        var onPart = element.Elements().FirstOrDefault(child =>
            child.Name.LocalName is "planItemOnPart" or "caseFileItemOnPart" or "onPart");
        var onPartRef = onPart is null
            ? string.Empty
            : Attribute(onPart, "sourceRef") ?? Attribute(onPart, "planItemRef")
                ?? Attribute(onPart, "caseFileItemRef") ?? string.Empty;
        var standardEvent = onPart is null
            ? string.Empty
            : Attribute(onPart, "standardEvent") ?? Attribute(onPart, "event")
                ?? onPart.Elements().FirstOrDefault(child => child.Name.LocalName == "standardEvent")?.Value.Trim()
                ?? string.Empty;
        var ifParts = element.Elements().Where(child => child.Name.LocalName is "ifPart" or "condition").ToList();
        var conditions = ifParts.Select(part => new SentryCondition(
            ExpressionText(part),
            Attribute(part, "variableRef") ?? string.Empty,
            standardEvent,
            Attribute(part, "logicalOperator") ?? "AND")).ToList();
        if (conditions.Count == 0 && onPart is not null)
            conditions.Add(new SentryCondition(string.Empty, string.Empty, standardEvent, "AND"));

        return new Sentry(id, conditions, onPartRef, true);
    }

    private static string ExpressionText(XElement part)
    {
        var expression = part.Descendants().FirstOrDefault(child =>
            child.Name.LocalName is "condition" or "body" or "expression");
        return (expression?.Value ?? part.Value).Trim();
    }

    private static Dictionary<string, string> Attributes(XElement element) =>
        element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration)
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value, StringComparer.Ordinal);

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string RequiredAttribute(XElement element, string localName, string message) =>
        Attribute(element, localName) is { Length: > 0 } value ? value : throw new CmmnParseException(message);
}
