using System.Xml.Linq;

namespace VertexBPMN.Domain.Entities.Modeling;

/// <summary>
/// DMN 1.4 Decision Table (minimal) using canonical record types DmnInput, DmnOutput, DmnRule.
/// Supports a subset of FEEL for equality and basic hit policies (UNIQUE, FIRST, ANY, COLLECT, RULE ORDER, PRIORITY, OUTPUT ORDER).
/// </summary>
public class DmnDecisionTable
{
    public string Key { get; }
    public string Name { get; }
    public IReadOnlyList<DmnInput> Inputs { get; }
    public IReadOnlyList<DmnOutput> Outputs { get; }
    public IReadOnlyList<DmnRule> Rules { get; }
    public string HitPolicy { get; }

    public DmnDecisionTable(
        string key,
        string name,
        IReadOnlyList<DmnInput> inputs,
        IReadOnlyList<DmnOutput> outputs,
        IReadOnlyList<DmnRule> rules,
        string hitPolicy)
    {
        Key = key;
        Name = name;
        Inputs = inputs;
        Outputs = outputs;
        Rules = rules;
        HitPolicy = hitPolicy;
    }

    /// <summary>
    /// Parses a minimal DMN decision table XML into a DmnDecisionTable using the shared record types.
    /// </summary>
    public static DmnDecisionTable Parse(string dmnXml)
    {
        var doc = XDocument.Parse(dmnXml);
        XNamespace ns = "http://www.omg.org/spec/DMN/20191111/MODEL/";
        var decision = doc.Descendants(ns + "decision").First();
        var key = (string?)decision.Attribute("id") ?? string.Empty;
        var name = (string?)decision.Attribute("name") ?? key;
        var table = decision.Descendants(ns + "decisionTable").First();
        var hitPolicy = (string?)table.Attribute("hitPolicy") ?? "UNIQUE";

        var inputs = table.Elements(ns + "input").Select(i =>
        {
            var id = (string?)i.Attribute("id") ?? string.Empty;
            var label = (string?)i.Attribute("label") ?? id;
            var typeRef = (string?)i.Element(ns + "inputExpression")?.Attribute("typeRef") ?? "string";
            return new DmnInput(id, label, typeRef);
        }).ToList();

        var outputs = table.Elements(ns + "output").Select(o =>
        {
            var id = (string?)o.Attribute("id") ?? string.Empty;
            var label = (string?)o.Attribute("name") ?? (string?)o.Attribute("label") ?? id;
            var typeRef = (string?)o.Attribute("typeRef") ?? "string";
            return new DmnOutput(id, label, typeRef);
        }).ToList();

        var rules = table.Elements(ns + "rule").Select((r, ruleIndex) =>
        {
            // Input conditions map inputId -> expression
            var inputConds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inputEntries = r.Elements(ns + "inputEntry").ToList();
            for (int i = 0; i < inputEntries.Count && i < inputs.Count; i++)
            {
                var expr = inputEntries[i].Value?.Trim() ?? string.Empty; // FEEL expression or '-'
                inputConds[inputs[i].Id] = expr;
            }
            // Output values map outputId -> raw string (object for future typed conversions)
            var outputVals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var outputEntries = r.Elements(ns + "outputEntry").ToList();
            for (int j = 0; j < outputEntries.Count && j < outputs.Count; j++)
            {
                var val = outputEntries[j].Value?.Trim() ?? string.Empty;
                outputVals[outputs[j].Id] = val;
            }
            return new DmnRule($"rule_{ruleIndex}", inputConds, outputVals);
        }).ToList();

        return new DmnDecisionTable(key, name, inputs, outputs, rules, hitPolicy);
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

    private Dictionary<string, object> EvaluateFirst(List<DmnRule> matches)
        => ProjectOutputs(matches[0]);

    private Dictionary<string, object> EvaluateAny(List<DmnRule> matches)
    {
        // All output values must be identical across matched rules
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
            result[output.Label] = matches.Select(m => m.OutputValues[output.Id]).ToList();
        return result;
    }

    private Dictionary<string, object> EvaluateRuleOrder(List<DmnRule> matches)
    {
        // Preserve rule order, aggregate outputs
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
            result[output.Label] = matches.Select(m => m.OutputValues[output.Id]).ToList();
        return result;
    }

    private Dictionary<string, object> EvaluatePriority(List<DmnRule> matches)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
        {
            // Lexicographic ordering as placeholder priority
            var min = matches.Select(m => m.OutputValues[output.Id]).OrderBy(v => v).First();
            result[output.Label] = min;
        }
        return result;
    }

    private Dictionary<string, object> EvaluateOutputOrder(List<DmnRule> matches)
    {
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
            result[output.Label] = matches.Select(m => m.OutputValues[output.Id]).OrderBy(v => v).ToList();
        return result;
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
        // Accept input.Id or input.Label as key
        if (vars.TryGetValue(input.Id, out var v)) return v;
        if (!string.IsNullOrEmpty(input.Label) && vars.TryGetValue(input.Label, out v)) return v;
        return null;
    }

    private static bool FeelMatches(string expression, object? value)
    {
        if (expression == "-" || string.IsNullOrWhiteSpace(expression)) return true; // wildcard / any
        if (expression.StartsWith("=", StringComparison.Ordinal))
            return string.Equals(expression.Substring(1).Trim(), value?.ToString(), StringComparison.OrdinalIgnoreCase);
        // simple equality
        return string.Equals(expression.Trim(), value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

