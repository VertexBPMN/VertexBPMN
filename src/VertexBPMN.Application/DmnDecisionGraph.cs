using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace VertexBPMN.Application;

/// <summary>
/// Validates and evaluates a local DMN decision-requirements graph. All boxed
/// expressions and unary tests pass through the same FEEL engine.
/// </summary>
public sealed class DmnDecisionGraph
{
    private static readonly IReadOnlySet<string> SupportedHitPolicies =
        new HashSet<string>(
            ["UNIQUE", "FIRST", "PRIORITY", "ANY", "COLLECT", "RULE ORDER", "OUTPUT ORDER"],
            StringComparer.Ordinal);

    private readonly XElement _root;
    private readonly XNamespace _namespace;
    private readonly IReadOnlyDictionary<string, XElement> _decisions;
    private readonly IReadOnlyDictionary<string, XElement> _decisionServices;
    private readonly IReadOnlyDictionary<string, XElement> _inputData;
    private readonly string _targetId;
    private readonly bool _targetIsDecisionService;

    private DmnDecisionGraph(XElement root, string deploymentKey)
    {
        _root = root;
        _namespace = root.Name.Namespace;
        _decisions = ElementsById("decision");
        _decisionServices = ElementsById("decisionService");
        _inputData = ElementsById("inputData");

        if (_decisions.ContainsKey(deploymentKey))
        {
            _targetId = deploymentKey;
        }
        else if (_decisionServices.ContainsKey(deploymentKey))
        {
            _targetId = deploymentKey;
            _targetIsDecisionService = true;
        }
        else if (_decisions.Count == 1 && _decisionServices.Count == 0)
        {
            _targetId = _decisions.Keys.Single();
        }
        else
        {
            throw new InvalidOperationException(
                $"DMN deployment key '{deploymentKey}' must identify a decision or decision service in a multi-element DRD.");
        }

        Validate();
    }

    public static DmnDecisionGraph Parse(string xml, string deploymentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentKey);
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
        ArgumentNullException.ThrowIfNull(inputs);
        var context = new Dictionary<string, object>(inputs, StringComparer.Ordinal);
        var cache = new Dictionary<string, DecisionEvaluation>(StringComparer.Ordinal);
        return _targetIsDecisionService
            ? EvaluateDecisionService(_decisionServices[_targetId], context, cache)
            : EvaluateDecision(_targetId, context, cache).Variables;
    }

    private Dictionary<string, object> EvaluateDecisionService(
        XElement service,
        Dictionary<string, object> context,
        Dictionary<string, DecisionEvaluation> cache)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var decisionId in DecisionServiceReferences(service, "outputDecision"))
        {
            var evaluation = EvaluateDecision(decisionId, context, cache);
            foreach (var output in evaluation.Variables) result[output.Key] = output.Value;
            result[DecisionBindingName(_decisions[decisionId])] = evaluation.Value!;
        }
        return result;
    }

    private DecisionEvaluation EvaluateDecision(
        string id,
        Dictionary<string, object> context,
        Dictionary<string, DecisionEvaluation> cache)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        var decision = _decisions[id];
        foreach (var dependency in RequiredDecisions(decision))
        {
            var dependencyEvaluation = EvaluateDecision(dependency, context, cache);
            AddToContext(_decisions[dependency], dependencyEvaluation, context);
        }

        var variables = decision.Element(_namespace + "decisionTable") is { } table
            ? EvaluateDecisionTable(decision, table, context)
            : EvaluateLiteralExpression(decision, context);
        var value = variables.Count == 1
            ? variables.Values.Single()
            : variables.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var evaluation = new DecisionEvaluation(variables, value);
        cache[id] = evaluation;
        AddToContext(decision, evaluation, context);
        return evaluation;
    }

    private Dictionary<string, object> EvaluateLiteralExpression(
        XElement decision,
        IReadOnlyDictionary<string, object> context)
    {
        var expression = ExpressionText(decision.Element(_namespace + "literalExpression")!);
        var value = FeelEvaluator.EvaluateExpression(expression, context);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [DecisionBindingName(decision)] = value!
        };
    }

    private Dictionary<string, object> EvaluateDecisionTable(
        XElement decision,
        XElement table,
        IReadOnlyDictionary<string, object> context)
    {
        var inputs = table.Elements(_namespace + "input").Select(input => new InputClause(
            RequiredAttribute(input, "id"),
            (string?)input.Attribute("label") ?? RequiredAttribute(input, "id"),
            InputExpression(input))).ToArray();
        var outputs = table.Elements(_namespace + "output").Select(output => new OutputClause(
            RequiredAttribute(output, "id"),
            (string?)output.Attribute("name") ?? (string?)output.Attribute("label") ?? RequiredAttribute(output, "id"),
            OutputPriorities(output, context))).ToArray();
        var inputValues = inputs.Select(input => EvaluateInputExpression(input, context)).ToArray();
        var matches = new List<Dictionary<string, object>>();

        foreach (var rule in table.Elements(_namespace + "rule"))
        {
            var inputEntries = rule.Elements(_namespace + "inputEntry").ToArray();
            var matchesRule = true;
            for (var index = 0; index < inputs.Length; index++)
            {
                var unaryTests = ExpressionText(inputEntries[index], "-");
                if (string.IsNullOrWhiteSpace(unaryTests) || unaryTests == "-") continue;
                if (!FeelEvaluator.EvaluateUnaryTests(unaryTests, inputValues[index], context))
                {
                    matchesRule = false;
                    break;
                }
            }
            if (!matchesRule) continue;

            var outputEntries = rule.Elements(_namespace + "outputEntry").ToArray();
            var outputValues = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var index = 0; index < outputs.Length; index++)
            {
                var expression = ExpressionText(outputEntries[index], "null");
                outputValues[outputs[index].Name] = EvaluateOutputExpression(expression, context)!;
            }
            matches.Add(outputValues);
        }

        if (matches.Count == 0) return new Dictionary<string, object>(StringComparer.Ordinal);
        var hitPolicy = ((string?)table.Attribute("hitPolicy") ?? "UNIQUE").Trim().ToUpperInvariant();
        var aggregation = ((string?)table.Attribute("aggregation"))?.Trim().ToUpperInvariant();
        return hitPolicy switch
        {
            "UNIQUE" => matches.Count == 1
                ? matches[0]
                : throw new InvalidOperationException("UNIQUE hit policy violated: multiple rules match."),
            "FIRST" => matches[0],
            "ANY" => EvaluateAny(matches),
            "COLLECT" => EvaluateCollect(outputs, matches, aggregation),
            "RULE ORDER" => EvaluateRuleOrder(outputs, matches),
            "PRIORITY" => matches.OrderBy(match => Priority(match, outputs), IntArrayComparer.Instance).First(),
            "OUTPUT ORDER" => EvaluateOutputOrder(outputs, matches),
            _ => throw new InvalidOperationException($"Unsupported DMN hit policy '{hitPolicy}'.")
        };
    }

    private static Dictionary<string, object> EvaluateAny(IReadOnlyList<Dictionary<string, object>> matches)
    {
        if (matches.Skip(1).Any(match => !StructuralEquals(matches[0], match)))
            throw new InvalidOperationException("ANY hit policy violated: matching rules have different outputs.");
        return matches[0];
    }

    private static Dictionary<string, object> EvaluateCollect(
        IReadOnlyList<OutputClause> outputs,
        IReadOnlyList<Dictionary<string, object>> matches,
        string? aggregation)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            var values = matches.Select(match => match[output.Name]).ToList();
            result[output.Name] = aggregation switch
            {
                "COUNT" => values.Count,
                "SUM" => values.Sum(ToDecimal),
                "MIN" => values.Min(ToDecimal),
                "MAX" => values.Max(ToDecimal),
                _ => values
            };
        }
        return result;
    }

    private static Dictionary<string, object> EvaluateRuleOrder(
        IReadOnlyList<OutputClause> outputs,
        IReadOnlyList<Dictionary<string, object>> matches) =>
        outputs.ToDictionary(
            output => output.Name,
            output => (object)matches.Select(match => match[output.Name]).ToList(),
            StringComparer.Ordinal);

    private static Dictionary<string, object> EvaluateOutputOrder(
        IReadOnlyList<OutputClause> outputs,
        IReadOnlyList<Dictionary<string, object>> matches)
    {
        var ordered = matches.OrderBy(match => Priority(match, outputs), IntArrayComparer.Instance).ToArray();
        return EvaluateRuleOrder(outputs, ordered);
    }

    private static int[] Priority(Dictionary<string, object> match, IReadOnlyList<OutputClause> outputs) =>
        outputs.Select(output =>
        {
            if (output.Priorities.Count == 0) return 0;
            var rank = output.Priorities.ToList().FindIndex(value => StructuralEquals(value, match[output.Name]));
            return rank < 0 ? int.MaxValue : rank;
        }).ToArray();

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
            foreach (var dependency in RequiredDecisions(decision))
                if (!_decisions.ContainsKey(dependency))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required decision '{dependency}'.");
            foreach (var input in RequiredInputs(decision))
                if (!_inputData.ContainsKey(input))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required input '{input}'.");

            if (decision.Element(_namespace + "decisionTable") is { } table)
                ValidateDecisionTable(id, table);
            else
                FeelEvaluator.ValidateExpression(ExpressionText(decision.Element(_namespace + "literalExpression")!));
        }

        foreach (var service in _decisionServices.Values)
        {
            var serviceId = RequiredAttribute(service, "id");
            var outputs = DecisionServiceReferences(service, "outputDecision").ToArray();
            if (outputs.Length == 0)
                throw new InvalidOperationException($"Decision service '{serviceId}' requires at least one outputDecision.");
            foreach (var decisionId in DecisionServiceReferences(service, "outputDecision")
                         .Concat(DecisionServiceReferences(service, "encapsulatedDecision")))
                if (!_decisions.ContainsKey(decisionId))
                    throw new InvalidOperationException(
                        $"Decision service '{serviceId}' references unknown decision '{decisionId}'.");
            foreach (var inputId in DecisionServiceReferences(service, "inputData"))
                if (!_inputData.ContainsKey(inputId))
                    throw new InvalidOperationException(
                        $"Decision service '{serviceId}' references unknown input data '{inputId}'.");
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
                throw new InvalidOperationException($"DMN DRD contains a dependency cycle at decision '{id}'.");
            foreach (var dependency in RequiredDecisions(_decisions[id])) Visit(dependency);
            visiting.Remove(id);
            visited.Add(id);
        }
        foreach (var id in _decisions.Keys) Visit(id);
    }

    private void ValidateDecisionTable(string decisionId, XElement table)
    {
        var hitPolicy = ((string?)table.Attribute("hitPolicy") ?? "UNIQUE").Trim().ToUpperInvariant();
        if (!SupportedHitPolicies.Contains(hitPolicy))
            throw new InvalidOperationException($"Unsupported DMN hit policy '{hitPolicy}'.");
        var aggregation = ((string?)table.Attribute("aggregation"))?.Trim().ToUpperInvariant();
        if (aggregation is not null && aggregation is not ("SUM" or "MIN" or "MAX" or "COUNT"))
            throw new InvalidOperationException($"Unsupported DMN COLLECT aggregation '{aggregation}'.");
        if (aggregation is not null && hitPolicy != "COLLECT")
            throw new InvalidOperationException("DMN aggregation is only valid with the COLLECT hit policy.");

        var inputs = table.Elements(_namespace + "input").ToArray();
        var outputs = table.Elements(_namespace + "output").ToArray();
        var rules = table.Elements(_namespace + "rule").ToArray();
        if (inputs.Length == 0 || outputs.Length == 0 || rules.Length == 0)
            throw new InvalidOperationException(
                $"Decision table '{decisionId}' requires at least one input, output, and rule.");
        if (aggregation is not null && outputs.Length != 1)
            throw new InvalidOperationException("DMN COLLECT aggregation requires exactly one output clause.");
        if (rules.Any(rule => rule.Elements(_namespace + "inputEntry").Count() != inputs.Length
                              || rule.Elements(_namespace + "outputEntry").Count() != outputs.Length))
            throw new InvalidOperationException("Every DMN rule must provide one entry per input and output.");

        foreach (var input in inputs) FeelEvaluator.ValidateExpression(InputExpression(input));
        foreach (var rule in rules)
        {
            foreach (var inputEntry in rule.Elements(_namespace + "inputEntry"))
            {
                var unaryTests = ExpressionText(inputEntry, "-");
                if (!string.IsNullOrWhiteSpace(unaryTests) && unaryTests != "-")
                    FeelEvaluator.ValidateUnaryTests(unaryTests);
            }
            foreach (var outputEntry in rule.Elements(_namespace + "outputEntry"))
            {
                var expression = ExpressionText(outputEntry, "null");
                if (!IsLegacyBareOutput(expression)) FeelEvaluator.ValidateExpression(expression);
            }
        }
        if (hitPolicy is "PRIORITY" or "OUTPUT ORDER"
            && outputs.All(output => string.IsNullOrWhiteSpace(output.Element(_namespace + "outputValues")?.Value)))
            throw new InvalidOperationException($"DMN {hitPolicy} requires ordered outputValues.");
    }

    private IReadOnlyDictionary<string, XElement> ElementsById(string localName) =>
        _root.Elements(_namespace + localName)
            .ToDictionary(element => RequiredAttribute(element, "id"), StringComparer.Ordinal);

    private IEnumerable<string> RequiredDecisions(XElement decision) =>
        decision.Elements(_namespace + "informationRequirement")
            .Select(requirement => requirement.Element(_namespace + "requiredDecision"))
            .Where(required => required is not null)
            .Select(required => LocalReference(required!, "href"));

    private IEnumerable<string> RequiredInputs(XElement decision) =>
        decision.Elements(_namespace + "informationRequirement")
            .Select(requirement => requirement.Element(_namespace + "requiredInput"))
            .Where(required => required is not null)
            .Select(required => LocalReference(required!, "href"));

    private IEnumerable<string> DecisionServiceReferences(XElement service, string localName) =>
        service.Elements(_namespace + localName).Select(reference => LocalReference(reference, "href"));

    private string InputExpression(XElement input)
    {
        var expression = input.Element(_namespace + "inputExpression");
        var text = expression?.Element(_namespace + "text")?.Value ?? expression?.Value;
        return string.IsNullOrWhiteSpace(text)
            ? (string?)input.Attribute("label") ?? RequiredAttribute(input, "id")
            : text.Trim();
    }

    private static object? EvaluateInputExpression(
        InputClause input,
        IReadOnlyDictionary<string, object> context)
    {
        if (IsSimpleFeelName(input.Expression) && !context.ContainsKey(input.Expression))
        {
            // Older VertexBPMN API clients bind decision-table inputs by the
            // clause id/label. Preserve that public contract while preferring
            // the standard FEEL input expression whenever its variable exists.
            if (context.TryGetValue(input.Id, out var value)) return value;
            if (context.TryGetValue(input.Label, out value)) return value;
        }

        return FeelEvaluator.EvaluateExpression(input.Expression, context);
    }

    private IReadOnlyList<object?> OutputPriorities(
        XElement output,
        IReadOnlyDictionary<string, object> context)
    {
        var values = output.Element(_namespace + "outputValues")?.Element(_namespace + "text")?.Value
                     ?? output.Element(_namespace + "outputValues")?.Value;
        if (string.IsNullOrWhiteSpace(values)) return [];
        var evaluated = FeelEvaluator.EvaluateExpression($"[{values}]", context);
        return evaluated is IEnumerable sequence and not string
            ? sequence.Cast<object?>().ToArray()
            : [evaluated];
    }

    private static object? EvaluateOutputExpression(
        string expression,
        IReadOnlyDictionary<string, object> context) =>
        IsLegacyBareOutput(expression) && !context.ContainsKey(expression)
            ? expression
            : FeelEvaluator.EvaluateExpression(expression, context);

    private static bool IsLegacyBareOutput(string expression) =>
        IsSimpleFeelName(expression)
        && expression is not ("true" or "false" or "null");

    private static bool IsSimpleFeelName(string expression) =>
        Regex.IsMatch(expression, @"^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant);

    private static void AddToContext(
        XElement decision,
        DecisionEvaluation evaluation,
        IDictionary<string, object> context)
    {
        foreach (var output in evaluation.Variables) context[output.Key] = output.Value;
        context[DecisionBindingName(decision)] = evaluation.Value!;
    }

    private static string DecisionBindingName(XElement decision) =>
        (string?)decision.Elements().FirstOrDefault(element => element.Name.LocalName == "variable")?.Attribute("name")
        ?? (string?)decision.Attribute("name")
        ?? RequiredAttribute(decision, "id");

    private static string ExpressionText(XElement expression, string? fallback = null)
    {
        var text = expression.Elements().FirstOrDefault(element => element.Name.LocalName == "text")?.Value
                   ?? expression.Value;
        if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        return fallback ?? throw new InvalidOperationException($"DMN {expression.Name.LocalName} requires FEEL text.");
    }

    private static string LocalReference(XElement element, string attribute)
    {
        var reference = RequiredAttribute(element, attribute);
        if (!reference.StartsWith('#'))
            throw new InvalidOperationException($"Only local DMN references are supported, got '{reference}'.");
        return reference[1..];
    }

    private static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"DMN {element.Name.LocalName} requires attribute '{name}'.");

    private static decimal ToDecimal(object value) =>
        decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new InvalidOperationException($"COLLECT aggregation requires numeric values, got '{value}'.");

    private static bool StructuralEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left is IDictionary leftDictionary && right is IDictionary rightDictionary)
        {
            if (leftDictionary.Count != rightDictionary.Count) return false;
            foreach (var key in leftDictionary.Keys)
                if (!rightDictionary.Contains(key) || !StructuralEquals(leftDictionary[key], rightDictionary[key]))
                    return false;
            return true;
        }
        if (left is IEnumerable leftSequence && right is IEnumerable rightSequence
            && left is not string && right is not string)
            return leftSequence.Cast<object?>().SequenceEqual(
                rightSequence.Cast<object?>(), StructuralEqualityComparer.Instance);
        if (decimal.TryParse(left.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber)
            && decimal.TryParse(right.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
            return leftNumber == rightNumber;
        return Equals(left, right);
    }

    private sealed record InputClause(string Id, string Label, string Expression);
    private sealed record OutputClause(string Id, string Name, IReadOnlyList<object?> Priorities);
    private sealed record DecisionEvaluation(Dictionary<string, object> Variables, object? Value);

    private sealed class IntArrayComparer : IComparer<int[]>
    {
        public static readonly IntArrayComparer Instance = new();
        public int Compare(int[]? left, int[]? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0) return comparison;
            }
            return left.Length.CompareTo(right.Length);
        }
    }

    private sealed class StructuralEqualityComparer : IEqualityComparer<object?>
    {
        public static readonly StructuralEqualityComparer Instance = new();
        public new bool Equals(object? left, object? right) => StructuralEquals(left, right);
        public int GetHashCode(object? value) => value?.GetHashCode() ?? 0;
    }
}
