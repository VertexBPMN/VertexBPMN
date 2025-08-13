using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VertexBPMN.Core.Services.Dmn;

/// <summary>
/// Minimal DMN 1.4 Decision Table parser and evaluator (single output, unique hit policy, FEEL subset).
/// </summary>
public class DmnDecisionTable
{
    public string Key { get; }
    public string Name { get; }
    public List<DmnInput> Inputs { get; }
    public List<DmnOutput> Outputs { get; }
    public List<DmnRule> Rules { get; }
    public string HitPolicy { get; }

    public DmnDecisionTable(string key, string name, List<DmnInput> inputs, List<DmnOutput> outputs, List<DmnRule> rules, string hitPolicy)
    {
        Key = key;
        Name = name;
        Inputs = inputs;
        Outputs = outputs;
        Rules = rules;
        HitPolicy = hitPolicy;
    }

    public static DmnDecisionTable Parse(string dmnXml)
    {
        var doc = XDocument.Parse(dmnXml);
        XNamespace ns = "http://www.omg.org/spec/DMN/20191111/MODEL/";
        var decision = doc.Descendants(ns + "decision").First();
        var key = (string?)decision.Attribute("id") ?? "";
        var name = (string?)decision.Attribute("name") ?? key;
        var table = decision.Descendants(ns + "decisionTable").First();
        var hitPolicy = (string?)table.Attribute("hitPolicy") ?? "UNIQUE";
        var inputs = table.Elements(ns + "input").Select(i => new DmnInput((string?)i.Attribute("id") ?? "", (string?)i.Element(ns + "inputExpression")?.Value ?? "")).ToList();
        var outputs = table.Elements(ns + "output").Select(o => new DmnOutput((string?)o.Attribute("id") ?? "", (string?)o.Attribute("name") ?? "output")).ToList();
        var rules = table.Elements(ns + "rule").Select(r =>
            new DmnRule(
                r.Elements(ns + "inputEntry").Select(e => (string?)e.Value ?? "").ToList(),
                r.Elements(ns + "outputEntry").Select(e => (string?)e.Value ?? "").ToList()
            )).ToList();
        return new DmnDecisionTable(key, name, inputs, outputs, rules, hitPolicy);
    }

    public Dictionary<string, object> Evaluate(Dictionary<string, object> inputs)
    {
        var matches = new List<DmnRule>();
        foreach (var rule in Rules)
        {
            bool match = true;
            for (int i = 0; i < rule.InputEntries.Count; i++)
            {
                var expr = rule.InputEntries[i];
                var inputName = Inputs[i].Id;
                if (!inputs.TryGetValue(inputName, out var value)) { match = false; break; }
                if (!FeelEquals(expr, value)) { match = false; break; }
            }
            if (match)
                matches.Add(rule);
        }
        if (matches.Count == 0)
            return new Dictionary<string, object>();

        // Hit Policy: UNIQUE (default), FIRST, ANY, COLLECT, RULE ORDER, PRIORITY, OUTPUT ORDER
        var policy = (HitPolicy ?? "UNIQUE").ToUpperInvariant();
        if (policy == "UNIQUE")
        {
            if (matches.Count > 1)
                throw new InvalidOperationException("UNIQUE hit policy violated: multiple rules match");
            var selected = matches.First();
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
                result[Outputs[j].Name] = selected.OutputEntries[j];
            return result;
        }
        if (policy == "FIRST")
        {
            var selected = matches.First();
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
                result[Outputs[j].Name] = selected.OutputEntries[j];
            return result;
        }
        if (policy == "ANY")
        {
            // All outputs for the same input combination must be identical, else error
            for (int j = 0; j < Outputs.Count; j++)
            {
                var first = matches[0].OutputEntries[j];
                foreach (var m in matches)
                {
                    if (!Equals(m.OutputEntries[j], first))
                        throw new InvalidOperationException("ANY hit policy violated: outputs differ for same input");
                }
            }
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
                result[Outputs[j].Name] = matches[0].OutputEntries[j];
            return result;
        }
        if (policy == "COLLECT")
        {
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
                result[Outputs[j].Name] = matches.Select(m => (object)m.OutputEntries[j]).ToList();
            return result;
        }
        if (policy == "RULE ORDER")
        {
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
                result[Outputs[j].Name] = matches.Select(m => (object)m.OutputEntries[j]).ToList();
            return result;
        }
        if (policy == "PRIORITY")
        {
            // For simplicity: lexicographical order, lowest wins (A < B < C)
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
            {
                var min = matches.Select(m => m.OutputEntries[j]).OrderBy(x => x).First();
                result[Outputs[j].Name] = min;
            }
            return result;
        }
        if (policy == "OUTPUT ORDER")
        {
            var result = new Dictionary<string, object>();
            for (int j = 0; j < Outputs.Count; j++)
            {
                var sorted = matches.Select(m => (object)m.OutputEntries[j]).OrderBy(x => x).ToList();
                result[Outputs[j].Name] = sorted;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    private static bool FeelEquals(string expr, object value)
    {
        // Minimal FEEL: '=' or value match
        if (expr == "-") return true;
        if (expr.StartsWith("=", StringComparison.Ordinal))
            return expr.Substring(1).Trim() == value?.ToString();
        return expr.Trim() == value?.ToString();
    }
}

public record DmnInput(string Id, string Expression);
public record DmnOutput(string Id, string Name);
public record DmnRule(List<string> InputEntries, List<string> OutputEntries);
