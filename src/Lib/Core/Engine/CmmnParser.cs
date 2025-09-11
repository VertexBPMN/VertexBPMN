using System.Xml.Linq;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Core.Exceptions;
using VertexBPMN.Core.Modeling;

namespace VertexBPMN.Core.Engine;

public class CmmnParser : ICmmnParser
{
    public async Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await Task.Run(() => XDocument.Parse(cmmnXml), cancellationToken);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.Get("https://www.omg.org/spec/CMMN/20151109/MODEL");
            var caseElement = doc.Descendants(ns + "case").FirstOrDefault()
                ?? throw new CmmnParseException("No case found in CMMN XML");

            var caseId = caseElement.Attribute("id")?.Value ?? throw new CmmnParseException("Case ID missing");
            var caseName = caseElement.Attribute("name")?.Value ?? caseId;

            var planItems = caseElement.Descendants(ns + "planItem").Select(item => new PlanItem(
                item.Attribute("id")?.Value ?? "",
                item.Element(ns + "definitionRef")?.Name.LocalName ?? "",
                item.Attribute("definitionRef")?.Value ?? "",
                item.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value),
                item.Descendants(ns + "entryCriterion").Select(c => c.Attribute("sentryRef")?.Value ?? "").ToList(),
                item.Descendants(ns + "exitCriterion").Select(c => c.Attribute("sentryRef")?.Value ?? "").ToList(),
                item.Attribute("isDiscretionary")?.Value == "true"
            )).ToList();

            var sentries = caseElement.Descendants(ns + "sentry").Select(sentry => new Sentry(
                sentry.Attribute("id")?.Value ?? "",
                sentry.Descendants(ns + "condition").Select(c => new SentryCondition(
                    c.Element(ns + "expression")?.Value ?? "",
                    c.Attribute("variableRef")?.Value ?? "",
                    c.Attribute("onPartEvent")?.Value ?? "",
                    c.Attribute("logicalOperator")?.Value ?? ""

                )).ToList(),
                sentry.Element(ns + "onPart")?.Attribute("planItemRef")?.Value ?? "",
                sentry.Element(ns + "entryCriterion") != null
            )).ToList();

            var caseFileItems = caseElement.Descendants(ns + "caseFileItem").Select(item => new CaseFileItem(
                item.Attribute("id")?.Value ?? "",
                item.Attribute("name")?.Value ?? "",
                null
            )).ToList();

            return await Task.FromResult(new CaseModel(caseId, caseName, planItems, sentries, caseFileItems));
        }
        catch (Exception ex)
        {
            throw new CmmnParseException("Failed to parse CMMN XML", ex);
        }
    }
}