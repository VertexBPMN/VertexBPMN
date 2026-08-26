using System.Xml;
using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Dmn;

/// <summary>
/// DMN 1.4 Decision Table (minimal) using canonical record types DmnInput, DmnOutput, DmnRule.
/// Supports a subset of FEEL for equality and basic hit policies (UNIQUE, FIRST, ANY, COLLECT, RULE ORDER, PRIORITY, OUTPUT ORDER).
/// Now EF-friendly (parameterless ctor + settable properties) so it can be persisted directly as JSON-converted collections.
/// </summary>
public class DmnDecisionTable
{
    public static readonly IReadOnlySet<string> SupportedApiHitPolicies =
        new HashSet<string>(["UNIQUE", "FIRST", "ANY", "COLLECT", "RULE ORDER"], StringComparer.Ordinal);

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<DmnInput> Inputs { get; set; } = new();
    public List<DmnOutput> Outputs { get; set; } = new();
    public List<DmnRule> Rules { get; set; } = new();
    public string HitPolicy { get; set; } = "UNIQUE";

    // Parameterless ctor for EF / serializers
    public DmnDecisionTable() { }

    public DmnDecisionTable(
        string key,
        string name,
        List<DmnInput> inputs,
        List<DmnOutput> outputs,
        List<DmnRule> rules,
        string hitPolicy)
    {
        Key = key;
        Name = name;
        Inputs = inputs.ToList();
        Outputs = outputs.ToList();
        Rules = rules.ToList();
        HitPolicy = hitPolicy;
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
            return new DmnOutput(id, label, typeRef);
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
                outputVals[outputs[j].Id] = val;
            }
            return new DmnRule($"rule_{ruleIndex}", inputConds, outputVals);
        }).ToList();

        if (inputs.Count == 0 || outputs.Count == 0 || rules.Count == 0)
            throw new InvalidOperationException("A DMN decision table requires at least one input, output, and rule.");
        if (rules.Any(rule => rule.InputConditions.Count != inputs.Count || rule.OutputValues.Count != outputs.Count))
            throw new InvalidOperationException("Every DMN rule must provide one entry per input and output.");

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
            result[output.Label] = matches.Select(m => m.OutputValues[output.Id]).ToList();
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
        var result = new Dictionary<string, object>();
        foreach (var output in Outputs)
        {
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
        if (vars.TryGetValue(input.Id, out var v)) return v;
        if (!string.IsNullOrEmpty(input.Label) && vars.TryGetValue(input.Label, out v)) return v;
        return null;
    }

    private static bool FeelMatches(string expression, object? value)
    {
        if (expression == "-" || string.IsNullOrWhiteSpace(expression)) return true;
        if (expression.StartsWith("=", StringComparison.Ordinal))
            return string.Equals(expression.Substring(1).Trim(), value?.ToString(), StringComparison.OrdinalIgnoreCase);
        return string.Equals(expression.Trim(), value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

