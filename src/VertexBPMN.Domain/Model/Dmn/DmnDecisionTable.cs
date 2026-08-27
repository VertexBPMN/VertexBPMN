using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Text.Json;

namespace VertexBPMN.Domain.Model.Dmn;

/// <summary>
/// DMN 1.4 Decision Table (minimal) using canonical record types DmnInput, DmnOutput, DmnRule.
/// Supports a subset of FEEL for equality and basic hit policies (UNIQUE, FIRST, ANY, COLLECT, RULE ORDER, PRIORITY, OUTPUT ORDER).
/// Now EF-friendly (parameterless ctor + settable properties) so it can be persisted directly as JSON-converted collections.
/// </summary>
public class DmnDecisionTable
{
    public static readonly IReadOnlySet<string> SupportedApiHitPolicies =
        new HashSet<string>(
            ["UNIQUE", "FIRST", "PRIORITY", "ANY", "COLLECT", "RULE ORDER", "OUTPUT ORDER"],
            StringComparer.Ordinal);

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<DmnInput> Inputs { get; set; } = new();
    public List<DmnOutput> Outputs { get; set; } = new();
    public List<DmnRule> Rules { get; set; } = new();
    public string HitPolicy { get; set; } = "UNIQUE";
    public string? Aggregation { get; set; }

    // Parameterless ctor for EF / serializers
    public DmnDecisionTable() { }

    public DmnDecisionTable(
        string key,
        string name,
        List<DmnInput> inputs,
        List<DmnOutput> outputs,
        List<DmnRule> rules,
        string hitPolicy,
        string? aggregation = null)
    {
        Key = key;
        Name = name;
        Inputs = inputs.ToList();
        Outputs = outputs.ToList();
        Rules = rules.ToList();
        HitPolicy = hitPolicy;
        Aggregation = aggregation;
    }

    /// <summary>
    /// Parses a minimal DMN decision table XML into a DmnDecisionTable using the shared record types.
    /// </summary>
    public static DmnDecisionTable Parse(string dmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dmnXml);
        using var input = new StringReader(dmnXml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10_000_000
        });
        var doc = XDocument.Load(reader, LoadOptions.None);
        var root = doc.Root ?? throw new InvalidOperationException("DMN definitions element is missing.");
        var namespaceName = root.Name.NamespaceName;
        if (namespaceName is not "http://www.omg.org/spec/DMN/20191111/MODEL/"
            and not "https://www.omg.org/spec/DMN/20191111/MODEL/")
            throw new InvalidOperationException($"Unsupported DMN namespace '{namespaceName}'.");

        XNamespace ns = root.Name.Namespace;
        var decisions = doc.Descendants(ns + "decision").ToList();
        if (decisions.Count != 1)
            throw new InvalidOperationException("The supported DMN subset requires exactly one decision per document.");
        var decision = decisions[0];
        var key = (string?)decision.Attribute("id");
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("DMN decision id is required.");
        var name = (string?)decision.Attribute("name") ?? key;
        var tables = decision.Elements(ns + "decisionTable").ToList();
        if (tables.Count != 1)
            throw new InvalidOperationException("The supported DMN subset requires exactly one decision table.");
        var table = tables[0];
        var hitPolicy = ((string?)table.Attribute("hitPolicy") ?? "UNIQUE").Trim().ToUpperInvariant();
        if (!SupportedApiHitPolicies.Contains(hitPolicy))
            throw new InvalidOperationException($"DMN hit policy '{hitPolicy}' is outside the supported API subset.");

        var inputs = table.Elements(ns + "input").Select(i =>
        {
            var id = (string?)i.Attribute("id");
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Every DMN input requires an id.");
            var label = (string?)i.Attribute("label") ?? id;
            var typeRef = (string?)i.Element(ns + "inputExpression")?.Attribute("typeRef") ?? "string";
            return new DmnInput(id, label, typeRef);
        }).ToList();

        var outputs = table.Elements(ns + "output").Select(o =>
        {
            var id = (string?)o.Attribute("id");
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Every DMN output requires an id.");
            var label = (string?)o.Attribute("name") ?? (string?)o.Attribute("label") ?? id;
            var typeRef = (string?)o.Attribute("typeRef") ?? "string";
            var allowedValues = ParseFeelList(i: o.Element(ns + "outputValues")?.Value);
            return new DmnOutput(id, label, typeRef, allowedValues);
        }).ToList();

        var rules = table.Elements(ns + "rule").Select((r, ruleIndex) =>
        {
            var inputConds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inputEntries = r.Elements(ns + "inputEntry").ToList();
            for (int i = 0; i < inputEntries.Count && i < inputs.Count; i++)
            {
                var expr = inputEntries[i].Value?.Trim() ?? string.Empty;
                inputConds[inputs[i].Id] = expr;
            }
            var outputVals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var outputEntries = r.Elements(ns + "outputEntry").ToList();
            for (int j = 0; j < outputEntries.Count && j < outputs.Count; j++)
            {
                var val = outputEntries[j].Value?.Trim() ?? string.Empty;
                outputVals[outputs[j].Id] = ParseFeelLiteral(val) ?? string.Empty;
            }
            return new DmnRule($"rule_{ruleIndex}", inputConds, outputVals);
        }).ToList();

        if (inputs.Count == 0 || outputs.Count == 0 || rules.Count == 0)
            throw new InvalidOperationException("A DMN decision table requires at least one input, output, and rule.");
        if (rules.Any(rule => rule.InputConditions.Count != inputs.Count || rule.OutputValues.Count != outputs.Count))
            throw new InvalidOperationException("Every DMN rule must provide one entry per input and output.");

        var aggregation = ((string?)table.Attribute("aggregation"))?.Trim().ToUpperInvariant();
        if (aggregation is not null && aggregation is not ("SUM" or "MIN" or "MAX" or "COUNT"))
            throw new InvalidOperationException($"Unsupported DMN COLLECT aggregation '{aggregation}'.");
        if (aggregation is not null && hitPolicy != "COLLECT")
            throw new InvalidOperationException("DMN aggregation is only valid with the COLLECT hit policy.");

        return new DmnDecisionTable(key, name, inputs, outputs, rules, hitPolicy, aggregation);
    }

    /// <summary>
    /// Evaluates the decision table for the given input variables (key = input Id or Label).
    /// </summary>
    public Dictionary<string, object> Evaluate(IReadOnlyDictionary<string, object> inputVariables)
    {
        var matching = new List<DmnRule>();
        foreach (var rule in Rules)
        {
            bool isMatch = true;
            foreach (var input in Inputs)
            {
                if (!rule.InputConditions.TryGetValue(input.Id, out var feelExpr)) { isMatch = false; break; }
                if (!FeelMatches(feelExpr, ResolveInputValue(input, inputVariables))) { isMatch = false; break; }
            }
            if (isMatch)
                matching.Add(rule);
        }

        if (matching.Count == 0)
            return new();

        var policy = (HitPolicy ?? "UNIQUE").ToUpperInvariant();
        return policy switch
        {
            "UNIQUE" => EvaluateUnique(matching),
            "FIRST" => EvaluateFirst(matching),
            "ANY" => EvaluateAny(matching),
            "COLLECT" => EvaluateCollect(matching),
            "RULE ORDER" => EvaluateRuleOrder(matching),
            "PRIORITY" => EvaluatePriority(matching),
            "OUTPUT ORDER" => EvaluateOutputOrder(matching),
            _ => EvaluateFirst(matching)
        };
    }

    private Dictionary<string, object> EvaluateUnique(List<DmnRule> matches)
    {
        if (matches.Count != 1)
            throw new InvalidOperationException("UNIQUE hit policy violated: multiple rules match");
        return ProjectOutputs(matches[0]);
    }

    private Dictionary<string, object> EvaluateFirst(List<DmnRule> matches) => ProjectOutputs(matches[0]);

    private Dictionary<string, object> EvaluateAny(List<DmnRule> matches)
    {
        foreach (var output in Outputs)
        {
            var first = matches[0].OutputValues[output.Id];
            foreach (var m in matches)
            {
                if (!Equals(m.OutputValues[output.Id], first))
                    throw new InvalidOperationException("ANY hit policy violated: outputs differ for same input");
            }
        }
        return ProjectOutputs(matches[0]);
    }

    private Dictionary<string, object> EvaluateCollect(List<DmnRule> matches)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
        {
            var values = matches.Select(m => m.OutputValues[output.Id]).ToList();
            result[output.Label] = Aggregation switch
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

    private Dictionary<string, object> EvaluateRuleOrder(List<DmnRule> matches)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
            result[output.Label] = matches.Select(m => m.OutputValues[output.Id]).ToList();
        return result;
    }

    private Dictionary<string, object> EvaluatePriority(List<DmnRule> matches)
    {
        return ProjectOutputs(matches.OrderBy(PriorityRank).First());
    }

    private Dictionary<string, object> EvaluateOutputOrder(List<DmnRule> matches)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
            result[output.Label] = matches.OrderBy(PriorityRank).Select(m => m.OutputValues[output.Id]).ToList();
        return result;
    }

    private int PriorityRank(DmnRule rule)
    {
        for (var outputIndex = 0; outputIndex < Outputs.Count; outputIndex++)
        {
            var output = Outputs[outputIndex];
            if (output.AllowedValues is not { Count: > 0 }) continue;
            var value = rule.OutputValues[output.Id];
            var rank = output.AllowedValues.ToList().FindIndex(allowed => FeelEquals(allowed, value));
            return rank < 0 ? int.MaxValue : (outputIndex * 10_000) + rank;
        }
        throw new InvalidOperationException($"DMN {HitPolicy} requires ordered outputValues.");
    }

    private Dictionary<string, object> ProjectOutputs(DmnRule rule)
    {
        var dict = new Dictionary<string, object>();
        foreach (var output in Outputs)
        {
            if (rule.OutputValues.TryGetValue(output.Id, out var v))
                dict[output.Label] = v;
        }
        return dict;
    }

    private static object? ResolveInputValue(DmnInput input, IReadOnlyDictionary<string, object> vars)
    {
        if (vars.TryGetValue(input.Id, out var v)) return v;
        if (!string.IsNullOrEmpty(input.Label) && vars.TryGetValue(input.Label, out v)) return v;
        return null;
    }

    private static bool FeelMatches(string expression, object? value)
    {
        if (expression == "-" || string.IsNullOrWhiteSpace(expression)) return true;
        expression = expression.Trim();
        if (expression.StartsWith("not(", StringComparison.OrdinalIgnoreCase) && expression.EndsWith(')'))
            return !FeelMatches(expression[4..^1], value);
        if ((expression.StartsWith('[') || expression.StartsWith('('))
            && (expression.EndsWith(']') || expression.EndsWith(')'))
            && expression.Contains("..", StringComparison.Ordinal))
        {
            var bounds = expression[1..^1].Split("..", 2, StringSplitOptions.TrimEntries);
            var lower = ParseFeelLiteral(bounds[0]);
            var upper = ParseFeelLiteral(bounds[1]);
            var lowerCompare = FeelCompare(value, lower);
            var upperCompare = FeelCompare(value, upper);
            return (expression[0] == '[' ? lowerCompare >= 0 : lowerCompare > 0)
                   && (expression[^1] == ']' ? upperCompare <= 0 : upperCompare < 0);
        }
        var alternatives = SplitFeelList(expression);
        if (alternatives.Count > 1)
            return alternatives.Any(alternative => FeelMatches(alternative, value));

        foreach (var op in new[] { "<=", ">=", "!=", "<", ">", "=" })
        {
            if (!expression.StartsWith(op, StringComparison.Ordinal)) continue;
            var literal = ParseFeelLiteral(expression[op.Length..].Trim());
            var comparison = FeelCompare(value, literal);
            return op switch
            {
                "<=" => comparison <= 0,
                ">=" => comparison >= 0,
                "!=" => comparison != 0,
                "<" => comparison < 0,
                ">" => comparison > 0,
                _ => comparison == 0
            };
        }
        return FeelEquals(value, ParseFeelLiteral(expression));
    }

    private static IReadOnlyList<object>? ParseFeelList(string? i)
        => string.IsNullOrWhiteSpace(i)
            ? null
            : SplitFeelList(i).Select(value => ParseFeelLiteral(value) ?? string.Empty).ToArray();

    private static List<string> SplitFeelList(string expression)
    {
        var values = new List<string>();
        var start = 0;
        var depth = 0;
        char quote = '\0';
        for (var index = 0; index < expression.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\')) quote = '\0';
                continue;
            }
            if (character is '\'' or '"') quote = character;
            else if (character is '(' or '[') depth++;
            else if (character is ')' or ']') depth--;
            else if (character == ',' && depth == 0)
            {
                values.Add(expression[start..index].Trim());
                start = index + 1;
            }
        }
        values.Add(expression[start..].Trim());
        return values;
    }

    private static object? ParseFeelLiteral(string expression)
    {
        expression = expression.Trim();
        if (expression.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (bool.TryParse(expression, out var boolean)) return boolean;
        if (decimal.TryParse(expression, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return number;
        if (expression.StartsWith("date(\"", StringComparison.OrdinalIgnoreCase) && expression.EndsWith("\")", StringComparison.Ordinal)
            && DateOnly.TryParse(expression[6..^2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (expression.Length >= 2
            && ((expression[0] == '"' && expression[^1] == '"')
                || (expression[0] == '\'' && expression[^1] == '\'')))
            return expression[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        return expression;
    }

    private static bool FeelEquals(object? left, object? right) => FeelCompare(left, right) == 0;

    private static int FeelCompare(object? left, object? right)
    {
        left = Normalize(left);
        right = Normalize(right);
        if (left is null || right is null) return left is null ? (right is null ? 0 : -1) : 1;
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
            return leftNumber.CompareTo(rightNumber);
        if (left is DateOnly leftDate && right is DateOnly rightDate) return leftDate.CompareTo(rightDate);
        if (left is bool leftBoolean && right is bool rightBoolean) return leftBoolean.CompareTo(rightBoolean);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    private static object? Normalize(object? value)
    {
        if (value is not JsonElement json) return value;
        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number when json.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => json.GetRawText()
        };
    }

    private static bool TryDecimal(object value, out decimal number)
        => decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    private static decimal ToDecimal(object value)
        => TryDecimal(Normalize(value) ?? 0, out var number)
            ? number
            : throw new InvalidOperationException($"COLLECT aggregation requires numeric values, got '{value}'.");
}

