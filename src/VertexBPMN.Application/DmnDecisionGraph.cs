using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Jint;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Application;

internal sealed class DmnDecisionGraph
{
    private readonly XElement _root;
    private readonly XNamespace _namespace;
    private readonly IReadOnlyDictionary<string, XElement> _decisions;
    private readonly string _targetDecisionId;

    private DmnDecisionGraph(XElement root, string deploymentKey)
    {
        _root = root;
        _namespace = root.Name.Namespace;
        _decisions = root.Elements(_namespace + "decision")
            .ToDictionary(decision => RequiredAttribute(decision, "id"), StringComparer.Ordinal);
        _targetDecisionId = _decisions.ContainsKey(deploymentKey)
            ? deploymentKey
            : _decisions.Count == 1
                ? _decisions.Keys.Single()
                : throw new InvalidOperationException(
                    $"DMN deployment key '{deploymentKey}' must identify the target decision in a multi-decision DRD.");
        Validate();
    }

    public static DmnDecisionGraph Parse(string xml, string deploymentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = 10_000_000
        });
        var document = XDocument.Load(reader);
        var root = document.Root ?? throw new InvalidOperationException("DMN definitions element is missing.");
        if (root.Name.LocalName != "definitions"
            || root.Name.NamespaceName is not ("http://www.omg.org/spec/DMN/20191111/MODEL/"
                or "https://www.omg.org/spec/DMN/20191111/MODEL/"))
            throw new InvalidOperationException($"Unsupported DMN namespace '{root.Name.NamespaceName}'.");
        return new DmnDecisionGraph(root, deploymentKey);
    }

    public Dictionary<string, object> Evaluate(IDictionary<string, object> inputs)
    {
        var context = new Dictionary<string, object>(inputs, StringComparer.Ordinal);
        var cache = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        return EvaluateDecision(_targetDecisionId, context, cache);
    }

    private Dictionary<string, object> EvaluateDecision(
        string id,
        Dictionary<string, object> context,
        Dictionary<string, Dictionary<string, object>> cache)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        var decision = _decisions[id];
        foreach (var dependency in Dependencies(decision))
        {
            var dependencyResult = EvaluateDecision(dependency, context, cache);
            foreach (var output in dependencyResult) context[output.Key] = output.Value;
        }

        Dictionary<string, object> result;
        if (decision.Element(_namespace + "decisionTable") is not null)
        {
            var standalone = new XDocument(new XElement(
                _root.Name,
                _root.Attributes(),
                _root.Elements().Where(element => element.Name.LocalName != "decision")
                    .Select(element => new XElement(element)),
                new XElement(decision)));
            result = DmnDecisionTable.Parse(standalone.ToString(SaveOptions.DisableFormatting)).Evaluate(context);
        }
        else if (decision.Element(_namespace + "literalExpression") is { } literal)
        {
            var expression = literal.Element(_namespace + "text")?.Value ?? literal.Value;
            var value = EvaluateFeelExpression(expression, context);
            var variable = decision.Element(_namespace + "variable");
            var outputName = (string?)variable?.Attribute("name")
                             ?? (string?)decision.Attribute("name")
                             ?? id;
            result = new Dictionary<string, object>(StringComparer.Ordinal) { [outputName] = value! };
        }
        else
        {
            throw new InvalidOperationException($"Decision '{id}' has no executable decisionTable or literalExpression.");
        }

        cache[id] = result;
        foreach (var output in result) context[output.Key] = output.Value;
        return result;
    }

    private void Validate()
    {
        if (_decisions.Count == 0) throw new InvalidOperationException("DMN document contains no decision.");
        foreach (var decision in _decisions.Values)
        {
            var id = RequiredAttribute(decision, "id");
            var executableCount = decision.Elements().Count(element =>
                element.Name.LocalName is "decisionTable" or "literalExpression");
            if (executableCount != 1)
                throw new InvalidOperationException(
                    $"Decision '{id}' must contain exactly one decisionTable or literalExpression.");
            foreach (var dependency in Dependencies(decision))
                if (!_decisions.ContainsKey(dependency))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required decision '{dependency}'.");
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidOperationException($"DMN DRD contains a dependency cycle at decision '{id}'.");
            foreach (var dependency in Dependencies(_decisions[id])) Visit(dependency);
            visiting.Remove(id);
            visited.Add(id);
        }
        Visit(_targetDecisionId);

        foreach (var decision in _decisions.Values.Where(item => item.Element(_namespace + "decisionTable") is not null))
        {
            var standalone = new XDocument(new XElement(
                _root.Name,
                _root.Attributes(),
                new XElement(decision)));
            _ = DmnDecisionTable.Parse(standalone.ToString(SaveOptions.DisableFormatting));
        }
    }

    private IEnumerable<string> Dependencies(XElement decision) =>
        decision.Elements(_namespace + "informationRequirement")
            .Select(requirement => requirement.Element(_namespace + "requiredDecision"))
            .Where(required => required is not null)
            .Select(required => ((string?)required!.Attribute("href") ?? string.Empty).TrimStart('#'))
            .Where(reference => reference.Length > 0);

    private static object? EvaluateFeelExpression(string rawExpression, IReadOnlyDictionary<string, object> context)
    {
        var expression = rawExpression.Trim();
        var ifMatch = Regex.Match(expression,
            @"^if\s+(.+?)\s+then\s+(.+?)\s+else\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (ifMatch.Success)
            expression = $"({ifMatch.Groups[1].Value}) ? ({ifMatch.Groups[2].Value}) : ({ifMatch.Groups[3].Value})";
        expression = Regex.Replace(expression, @"(?<![<>=!])=(?!=)", "==", RegexOptions.CultureInvariant);
        expression = Regex.Replace(expression, @"\band\b", "&&", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expression = Regex.Replace(expression, @"\bor\b", "||", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expression = Regex.Replace(expression, @"\bnot\s*\(", "!(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var engine = new Jint.Engine(options => options
            .TimeoutInterval(TimeSpan.FromMilliseconds(100))
            .LimitRecursion(64)
            .MaxStatements(1_000));
        foreach (var value in context) engine.SetValue(value.Key, Normalize(value.Value));
        var evaluated = engine.Evaluate(expression).ToObject();
        return Normalize(evaluated);
    }

    private static object? Normalize(object? value) =>
        value is JsonElement json
            ? json.ValueKind switch
            {
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => json.GetRawText()
            }
            : value;

    private static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"DMN {element.Name.LocalName} requires attribute '{name}'.");
}
