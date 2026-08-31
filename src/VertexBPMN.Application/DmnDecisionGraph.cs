using System.Collections;
using System.Globalization;
using System.Text.Json;
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
    private readonly IReadOnlyDictionary<string, XElement> _itemDefinitions;
    private readonly IReadOnlyDictionary<string, XElement> _businessKnowledgeModels;
    private readonly string _targetId;

    private DmnDecisionGraph(XElement root, string deploymentKey)
    {
        _root = root;
        _namespace = root.Name.Namespace;
        _decisions = ElementsById("decision");
        _decisionServices = ElementsById("decisionService");
        _inputData = ElementsById("inputData");
        _itemDefinitions = _root.Elements(_namespace + "itemDefinition")
            .Where(element => !string.IsNullOrWhiteSpace((string?)element.Attribute("name")))
            .ToDictionary(element => (string)element.Attribute("name")!, StringComparer.Ordinal);
        _businessKnowledgeModels = ElementsById("businessKnowledgeModel");

        if (_decisions.ContainsKey(deploymentKey))
        {
            _targetId = deploymentKey;
        }
        else if (_decisionServices.ContainsKey(deploymentKey))
        {
            _targetId = deploymentKey;
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
        return new DmnDecisionGraph(ParseDefinitions(xml), deploymentKey);
    }

    public static DmnDecisionGraph Parse(
        string xml,
        string deploymentKey,
        IEnumerable<string> importedModels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentKey);
        ArgumentNullException.ThrowIfNull(importedModels);
        var root = ParseDefinitions(xml);
        var candidates = importedModels
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(ParseDefinitions)
            .ToArray();
        var imports = ResolveImportClosure(root, candidates);
        return imports.Length == 0
            ? new DmnDecisionGraph(root, deploymentKey)
            : new DmnDecisionGraph(MergeDefinitions(root, imports), deploymentKey);
    }

    private static XElement[] ResolveImportClosure(XElement main, IReadOnlyList<XElement> candidates)
    {
        var candidatesByNamespace = candidates
            .GroupBy(candidate => RequiredAttribute(candidate, "namespace"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var selected = new List<XElement>();
        var visited = new HashSet<string>(StringComparer.Ordinal)
        {
            RequiredAttribute(main, "namespace")
        };
        var pending = new Queue<XElement>();
        pending.Enqueue(main);
        while (pending.Count > 0)
        {
            var owner = pending.Dequeue();
            foreach (var import in owner.Elements(owner.Name.Namespace + "import"))
            {
                var importNamespace = RequiredAttribute(import, "namespace");
                if (!visited.Add(importNamespace)) continue;
                if (!candidatesByNamespace.TryGetValue(importNamespace, out var matches))
                    throw new InvalidOperationException(
                        $"Imported DMN namespace '{importNamespace}' was not supplied.");
                if (matches.Length != 1)
                    throw new InvalidOperationException(
                        $"Imported DMN namespace '{importNamespace}' is ambiguous ({matches.Length} models supplied).");
                selected.Add(matches[0]);
                pending.Enqueue(matches[0]);
            }
        }
        return selected.ToArray();
    }

    private static XElement ParseDefinitions(string xml)
    {
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
                or "https://www.omg.org/spec/DMN/20191111/MODEL/"
                or "https://www.omg.org/spec/DMN/20230324/MODEL/"))
            throw new InvalidOperationException($"Unsupported DMN namespace '{root.Name.NamespaceName}'.");
        return root;
    }

    private static XElement MergeDefinitions(XElement main, IReadOnlyList<XElement> imported)
    {
        var roots = new[] { main }.Concat(imported).ToArray();
        var byNamespace = roots.ToDictionary(
            root => RequiredAttribute(root, "namespace"),
            StringComparer.Ordinal);
        var modelNamespace = main.Name.Namespace;
        var supportedElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "itemDefinition", "inputData", "decision", "decisionService",
            "businessKnowledgeModel", "knowledgeSource"
        };
        var idMap = new Dictionary<(string Namespace, string Id), string>();
        for (var modelIndex = 0; modelIndex < roots.Length; modelIndex++)
        {
            var root = roots[modelIndex];
            var ownerNamespace = RequiredAttribute(root, "namespace");
            foreach (var element in root.Elements().Where(element => supportedElements.Contains(element.Name.LocalName)))
            {
                var id = (string?)element.Attribute("id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                idMap[(ownerNamespace, id)] = modelIndex == 0 ? id : $"__vertexImport{modelIndex}_{id}";
            }
        }

        var merged = new XElement(main.Name, main.Attributes());
        foreach (var root in roots)
        {
            var ownerNamespace = RequiredAttribute(root, "namespace");
            var aliases = root.Elements(modelNamespace + "import")
                .Where(import => !string.IsNullOrWhiteSpace((string?)import.Attribute("name")))
                .ToDictionary(
                    import => RequiredAttribute(import, "name"),
                    import => RequiredAttribute(import, "namespace"),
                    StringComparer.Ordinal);
            foreach (var source in root.Elements().Where(element => supportedElements.Contains(element.Name.LocalName)))
            {
                var clone = new XElement(source);
                if ((string?)clone.Attribute("id") is { Length: > 0 } topId)
                    clone.SetAttributeValue("id", idMap[(ownerNamespace, topId)]);

                foreach (var reference in clone.DescendantsAndSelf().Attributes("href"))
                {
                    var href = reference.Value;
                    var fragmentIndex = href.LastIndexOf('#');
                    var targetNamespace = fragmentIndex == 0 ? ownerNamespace : href[..fragmentIndex];
                    var targetId = Uri.UnescapeDataString(href[(fragmentIndex + 1)..]);
                    if (!byNamespace.ContainsKey(targetNamespace)
                        || !idMap.TryGetValue((targetNamespace, targetId), out var mappedId))
                        throw new InvalidOperationException($"Imported DMN reference '{href}' could not be resolved.");
                    reference.Value = $"#{mappedId}";
                }

                foreach (var typeReference in clone.DescendantsAndSelf()
                             .Where(element => element.Name.LocalName == "typeRef"))
                    typeReference.Value = StripImportAlias(typeReference.Value, aliases.Keys);
                foreach (var typeReference in clone.DescendantsAndSelf().Attributes("typeRef"))
                    typeReference.Value = StripImportAlias(typeReference.Value, aliases.Keys);
                foreach (var text in clone.DescendantsAndSelf()
                             .Where(element => element.Name.LocalName == "text"))
                    text.Value = RewriteOutsideFeelStrings(
                        text.Value,
                        segment => aliases.Keys.Aggregate(
                            segment,
                            (current, alias) => current.Replace($"{alias}.", string.Empty, StringComparison.Ordinal)));
                merged.Add(clone);
            }
        }
        return merged;
    }

    private static string StripImportAlias(string typeReference, IEnumerable<string> aliases)
    {
        var trimmed = typeReference.Trim();
        foreach (var alias in aliases)
            if (trimmed.StartsWith($"{alias}.", StringComparison.Ordinal))
                return trimmed[(alias.Length + 1)..];
        return trimmed;
    }

    public Dictionary<string, object> Evaluate(IDictionary<string, object> inputs)
        => Evaluate(_targetId, inputs);

    /// <summary>
    /// Evaluates another decision or decision service from the same validated
    /// DRD without parsing and validating the model again.
    /// </summary>
    public Dictionary<string, object> Evaluate(
        string deploymentKey,
        IDictionary<string, object> inputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentKey);
        ArgumentNullException.ThrowIfNull(inputs);
        var context = NormalizeInputs(inputs);
        var cache = new Dictionary<string, DecisionEvaluation>(StringComparer.Ordinal);
        Dictionary<string, object> result;
        if (_decisions.ContainsKey(deploymentKey))
            result = EvaluateDecision(deploymentKey, context, cache).Variables;
        else if (_decisionServices.TryGetValue(deploymentKey, out var service))
            result = EvaluateDecisionService(service, context, cache);
        else
            throw new InvalidOperationException(
                $"DMN deployment key '{deploymentKey}' does not identify a decision or decision service in this DRD.");
        return result.ToDictionary(
            entry => entry.Key,
            entry => ToPublicValue(entry.Value)!,
            StringComparer.Ordinal);
    }

    private static object? ToPublicValue(object? value)
    {
        if (value is FeelTemporalValue temporal) return temporal.Value;
        if (value is IDictionary dictionary)
            return dictionary.Keys.Cast<object>().ToDictionary(
                key => key.ToString()!,
                key => ToPublicValue(dictionary[key])!,
                StringComparer.Ordinal);
        if (value is IEnumerable sequence and not string)
            return sequence.Cast<object?>().Select(ToPublicValue).ToList();
        return value;
    }

    private Dictionary<string, object> NormalizeInputs(IDictionary<string, object> inputs)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            var definition = _inputData.Values.FirstOrDefault(candidate =>
                string.Equals((string?)candidate.Attribute("name"), input.Key, StringComparison.Ordinal)
                || string.Equals(
                    (string?)candidate.Element(_namespace + "variable")?.Attribute("name"),
                    input.Key,
                    StringComparison.Ordinal));
            var typeRef = TypeReference(definition?.Element(_namespace + "variable"));
            result[input.Key] = CoerceValue(input.Value, typeRef)!;
        }
        return result;
    }

    private object? CoerceValue(object? value, string? typeRef)
    {
        if (value is null || value is FeelTemporalValue || string.IsNullOrWhiteSpace(typeRef)) return value;
        var type = typeRef.Split(':').Last();
        var temporalKind = type switch
        {
            "date" => "date",
            "time" => "time",
            "dateTime" or "date and time" => "date time",
            "dayTimeDuration" or "days and time duration" or "yearMonthDuration" or "years and months duration" => "duration",
            _ => null
        };
        if (temporalKind is not null && value is string lexical)
            return new FeelTemporalValue(temporalKind, lexical);

        if (!_itemDefinitions.TryGetValue(type, out var itemDefinition)) return value;
        if ((bool?)itemDefinition.Attribute("isCollection") == true
            && value is IEnumerable collection and not string)
        {
            var elementType = TypeReference(itemDefinition);
            return collection.Cast<object?>().Select(item => CoerceValue(item, elementType)).ToList();
        }

        var components = itemDefinition.Elements(_namespace + "itemComponent").ToArray();
        if (components.Length > 0 && value is IDictionary dictionary)
        {
            var normalized = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var key in dictionary.Keys.Cast<object>())
            {
                var name = key.ToString()!;
                var component = components.FirstOrDefault(candidate =>
                    string.Equals((string?)candidate.Attribute("name"), name, StringComparison.Ordinal));
                var componentType = TypeReference(component);
                var componentValue = dictionary[key];
                if ((bool?)component?.Attribute("isCollection") == true
                    && componentValue is IEnumerable componentCollection and not string)
                    normalized[name] = componentCollection.Cast<object?>()
                        .Select(item => CoerceValue(item, componentType))
                        .ToList();
                else
                    normalized[name] = CoerceValue(componentValue, componentType)!;
            }
            return normalized;
        }

        return CoerceValue(value, TypeReference(itemDefinition));
    }

    private string? TypeReference(XElement? element) =>
        (string?)element?.Attribute("typeRef")
        ?? element?.Element(_namespace + "typeRef")?.Value.Trim();

    private string ResolveItemDefinitionTypes(string expression)
    {
        if (!expression.Contains("instance of", StringComparison.OrdinalIgnoreCase)
            || _itemDefinitions.Count == 0)
            return expression;

        var result = expression;
        foreach (var name in _itemDefinitions.Keys.OrderByDescending(candidate => candidate.Length))
        {
            var resolved = ResolveFeelType(name, new HashSet<string>(StringComparer.Ordinal));
            result = Regex.Replace(
                result,
                $@"(?<![A-Za-z0-9_-]){Regex.Escape(name)}(?![A-Za-z0-9_-])",
                _ => resolved,
                RegexOptions.CultureInvariant);
        }
        return result;
    }

    private string ResolveFeelType(string typeRef, ISet<string> resolving)
    {
        var type = typeRef.Split(':').Last().Trim();
        if (type is "dateTime" or "date and time") return "date_time";
        if (type is "dayTimeDuration" or "days and time duration") return "days and time duration";
        if (type is "yearMonthDuration" or "years and months duration") return "years and months duration";
        if (!_itemDefinitions.TryGetValue(type, out var definition)) return type;
        if (!resolving.Add(type))
            throw new InvalidOperationException($"Recursive DMN item definition '{type}' is not supported.");

        try
        {
            var function = definition.Element(_namespace + "functionItem");
            if (function is not null)
            {
                var parameters = function.Elements(_namespace + "parameters")
                    .Select(parameter => ResolveFeelType(TypeReference(parameter) ?? "Any", resolving));
                var output = ResolveFeelType((string?)function.Attribute("outputTypeRef") ?? "Any", resolving);
                return $"function<{string.Join(", ", parameters)}> -> {output}";
            }

            var components = definition.Elements(_namespace + "itemComponent").ToArray();
            if (components.Length > 0)
            {
                var entries = components.Select(component =>
                    $"{RequiredAttribute(component, "name")}: "
                    + ResolveFeelType(TypeReference(component) ?? "Any", resolving));
                return $"context<{string.Join(", ", entries)}>";
            }

            var underlying = ResolveFeelType(TypeReference(definition) ?? "Any", resolving);
            return (bool?)definition.Attribute("isCollection") == true
                ? $"list<{underlying}>"
                : underlying;
        }
        finally
        {
            resolving.Remove(type);
        }
    }

    private Dictionary<string, object> EvaluateDecisionService(
        XElement service,
        Dictionary<string, object> context,
        Dictionary<string, DecisionEvaluation> cache)
    {
        var suppliedDecisions = PrepareDecisionServiceInputs(service, context, cache);
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var decisionId in DecisionServiceReferences(service, "outputDecision"))
        {
            var evaluation = EvaluateDecision(decisionId, context, cache, suppliedDecisions);
            EnsureValueMatchesType(
                DecisionBindingName(_decisions[decisionId]),
                evaluation.Value,
                DecisionServiceOutputType(service));
            foreach (var output in evaluation.Variables) result[output.Key] = output.Value;
            result[DecisionBindingName(_decisions[decisionId])] = evaluation.Value!;
        }
        return result;
    }

    private IReadOnlySet<string> PrepareDecisionServiceInputs(
        XElement service,
        Dictionary<string, object> context,
        Dictionary<string, DecisionEvaluation> cache)
    {
        foreach (var inputId in DecisionServiceReferences(service, "inputData"))
        {
            var input = _inputData[inputId];
            var name = DecisionBindingName(input);
            if (!context.TryGetValue(name, out var value))
                throw new InvalidOperationException($"Decision service input '{name}' was not supplied.");
            var type = TypeReference(input.Element(_namespace + "variable"));
            value = CoerceServiceInput(value, type);
            EnsureValueMatchesType(name, value, type);
            context[name] = value!;
        }

        var supplied = DecisionServiceReferences(service, "inputDecision").ToHashSet(StringComparer.Ordinal);
        foreach (var decisionId in supplied)
        {
            var decision = _decisions[decisionId];
            var name = DecisionBindingName(decision);
            if (!context.TryGetValue(name, out var value))
                throw new InvalidOperationException($"Decision service input '{name}' was not supplied.");
            var type = TypeReference(decision.Element(_namespace + "variable"));
            value = CoerceServiceInput(value, type);
            EnsureValueMatchesType(name, value, type);
            context[name] = value!;
            context[DecisionAlias(decisionId)] = value!;
            cache[decisionId] = new DecisionEvaluation(
                new Dictionary<string, object>(StringComparer.Ordinal) { [name] = value! },
                value);
        }
        return supplied;
    }

    private object? CoerceServiceInput(object? value, string? typeRef)
    {
        if (value is null || string.IsNullOrWhiteSpace(typeRef)) return value;
        var type = typeRef.Split(':').Last();
        if (_itemDefinitions.TryGetValue(type, out var definition)
            && (bool?)definition.Attribute("isCollection") == true)
        {
            var elementType = TypeReference(definition);
            return value is IEnumerable sequence and not string
                ? sequence.Cast<object?>().Select(item => CoerceValue(item, elementType)).ToList()
                : new List<object?> { CoerceValue(value, elementType) };
        }

        if (value is IEnumerable collection and not string)
        {
            var values = collection.Cast<object?>().ToArray();
            if (values.Length == 1) value = values[0];
        }
        return CoerceValue(value, typeRef);
    }

    private void EnsureValueMatchesType(string name, object? value, string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return;
        if (RequiresManagedStructuralType(type))
        {
            if (!ValueMatchesType(value, type))
                throw new InvalidOperationException($"Decision service input '{name}' does not match type '{type}'.");
            return;
        }
        var expression = ResolveItemDefinitionTypes($"__vertexInput instance of {type}");
        var matches = FeelEvaluator.EvaluateExpression(
            expression,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["__vertexInput"] = value! });
        if (matches is not true)
            throw new InvalidOperationException($"Decision service input '{name}' does not match type '{type}'.");
    }

    private DecisionEvaluation EvaluateDecision(
        string id,
        Dictionary<string, object> context,
        Dictionary<string, DecisionEvaluation> cache,
        IReadOnlySet<string>? suppliedDecisions = null)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        var decision = _decisions[id];
        foreach (var dependency in RequiredDecisions(decision))
        {
            var dependencyEvaluation = EvaluateDecision(dependency, context, cache, suppliedDecisions);
            AddToContext(_decisions[dependency], dependencyEvaluation, context);
        }

        var expression = DecisionExpression(decision);
        var variables = expression.Name.LocalName == "decisionTable"
            ? EvaluateDecisionTable(decision, expression, context)
            : EvaluateBoxedExpression(decision, expression, context, suppliedDecisions);
        var value = variables.Count == 1
            ? variables.Values.Single()
            : variables.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var evaluation = new DecisionEvaluation(variables, value);
        cache[id] = evaluation;
        AddToContext(decision, evaluation, context);
        return evaluation;
    }

    private Dictionary<string, object> EvaluateBoxedExpression(
        XElement decision,
        XElement expression,
        IReadOnlyDictionary<string, object> context,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var value = expression.Name.LocalName switch
        {
            "conditional" => EvaluateConditional(decision, expression, context, suppliedDecisions),
            "some" or "every" => EvaluateQuantified(decision, expression, context, suppliedDecisions),
            "filter" => EvaluateFilter(decision, expression, context, suppliedDecisions),
            _ => EvaluateFeel(decision, expression, context, suppliedDecisions)
        };
        var outputType = TypeReference(expression)
                         ?? TypeReference(decision.Element(_namespace + "variable"));
        if (RequiresManagedStructuralType(outputType))
        {
            value = CoerceValue(value, outputType);
            if (!ValueMatchesType(value, outputType))
            {
                var isContextFunction = expression.Name.LocalName == "literalExpression"
                                        && ExpressionText(expression).TrimStart().StartsWith(
                                            "context(", StringComparison.Ordinal);
                if (isContextFunction)
                    value = null;
                else
                    throw new InvalidOperationException(
                        $"Decision '{DecisionBindingName(decision)}' produced a value that does not match type '{outputType}'.");
            }
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [DecisionBindingName(decision)] = value!
        };
    }

    private object? EvaluateFeel(
        XElement decision,
        XElement expression,
        IReadOnlyDictionary<string, object> context,
        IReadOnlySet<string>? suppliedDecisions) =>
        FeelEvaluator.EvaluateExpression(
            CompileDecisionExpression(decision, expression, suppliedDecisions),
            context);

    private object? EvaluateConditional(
        XElement decision,
        XElement conditional,
        IReadOnlyDictionary<string, object> context,
        IReadOnlySet<string>? suppliedDecisions)
    {
        XElement Branch(string name)
        {
            var wrapper = conditional.Element(_namespace + name)
                          ?? throw new InvalidOperationException($"DMN conditional requires '{name}'.");
            return wrapper.Elements().SingleOrDefault(IsBoxedExpression)
                   ?? throw new InvalidOperationException(
                       $"DMN conditional '{name}' requires one boxed expression.");
        }

        var conditionExpression = BoxedExpressionToFeel(Branch("if"));
        var condition = FeelEvaluator.EvaluateExpression(
            CompileDecisionExpression(decision, conditionExpression, suppliedDecisions),
            context);
        if (condition is not bool booleanCondition)
            throw new InvalidOperationException("DMN conditional condition must evaluate to a boolean value.");
        var selected = Branch(booleanCondition ? "then" : "else");
        return FeelEvaluator.EvaluateExpression(
            CompileDecisionExpression(decision, selected, suppliedDecisions),
            context);
    }

    private object EvaluateQuantified(
        XElement decision,
        XElement quantified,
        IReadOnlyDictionary<string, object> context,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var inputWrapper = quantified.Element(_namespace + "in")!;
        var inputExpression = inputWrapper.Elements().Single(IsBoxedExpression);
        var input = EvaluateFeel(decision, inputExpression, context, suppliedDecisions);
        if (input is not IEnumerable sequence || input is string)
            throw new InvalidOperationException(
                $"DMN {quantified.Name.LocalName} input must evaluate to a list.");

        var predicateWrapper = quantified.Element(_namespace + "satisfies")!;
        var predicate = predicateWrapper.Elements().Single(IsBoxedExpression);
        var iterator = RequiredAttribute(quantified, "iteratorVariable");
        var isEvery = quantified.Name.LocalName == "every";
        foreach (var item in sequence)
        {
            var iterationContext = new Dictionary<string, object>(context, StringComparer.Ordinal)
            {
                [iterator] = item!
            };
            var result = EvaluateFeel(decision, predicate, iterationContext, suppliedDecisions);
            if (result is not bool booleanResult)
                throw new InvalidOperationException(
                    $"DMN {quantified.Name.LocalName} predicate must evaluate to a boolean value.");
            if (!isEvery && booleanResult) return true;
            if (isEvery && !booleanResult) return false;
        }

        return isEvery;
    }

    private object? EvaluateFilter(
        XElement decision,
        XElement filter,
        IReadOnlyDictionary<string, object> context,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var match = filter.Element(_namespace + "match")!.Elements().Single(IsBoxedExpression);
        if (match.Name.LocalName == "literalExpression")
        {
            var text = ExpressionText(match).Trim();
            if (text.Length >= 2 && text.StartsWith('"') && text.EndsWith('"'))
                throw new InvalidOperationException("DMN filter match expression must be boolean-compatible.");
        }

        return EvaluateFeel(decision, filter, context, suppliedDecisions);
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

        if (matches.Count == 0)
        {
            var defaults = table.Elements(_namespace + "output")
                .Select(output => output.Element(_namespace + "defaultOutputEntry"))
                .ToArray();
            if (defaults.Length == outputs.Length && defaults.All(entry => entry is not null))
            {
                return outputs.Select((output, index) => new
                    {
                        output.Name,
                        Value = EvaluateOutputExpression(ExpressionText(defaults[index]!, "null"), context)!
                    })
                    .ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);
            }
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
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
            var id = ElementIdentity(decision);
            var executableCount = decision.Elements().Count(IsBoxedExpression);
            if (executableCount > 1)
                throw new InvalidOperationException(
                    $"Decision '{id}' must contain at most one executable boxed expression.");
            foreach (var dependency in RequiredDecisions(decision))
                if (!_decisions.ContainsKey(dependency))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required decision '{dependency}'.");
            foreach (var input in RequiredInputs(decision))
                if (!_inputData.ContainsKey(input))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required input '{input}'.");
            foreach (var knowledge in RequiredKnowledge(decision))
                if (!_businessKnowledgeModels.ContainsKey(knowledge) && !_decisionServices.ContainsKey(knowledge))
                    throw new InvalidOperationException($"Decision '{id}' references unknown required knowledge '{knowledge}'.");

            if (executableCount == 0) continue;
            var expression = DecisionExpression(decision);
            try
            {
                if (expression.Name.LocalName == "decisionTable")
                    ValidateDecisionTable(id, expression);
                else
                    FeelEvaluator.ValidateExpression(RewriteDecisionReferences(
                        BoxedExpressionToFeel(expression), RequiredDecisions(decision)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Decision '{id}' contains invalid executable logic: {exception.Message}", exception);
            }
        }

        foreach (var model in _businessKnowledgeModels.Values)
        {
            var id = ElementIdentity(model);
            var logic = model.Element(_namespace + "encapsulatedLogic")
                        ?? throw new InvalidOperationException(
                            $"Business knowledge model '{id}' requires encapsulatedLogic.");
            var expression = logic.Elements().SingleOrDefault(IsBoxedExpression)
                             ?? throw new InvalidOperationException(
                                 $"Business knowledge model '{id}' requires one executable boxed expression.");
            try
            {
                if (expression.Name.LocalName == "decisionTable")
                    ValidateDecisionTable(id, expression);
                FeelEvaluator.ValidateExpression(BoxedExpressionToFeel(expression));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Business knowledge model '{id}' contains invalid executable logic: {exception.Message}", exception);
            }
        }

        foreach (var service in _decisionServices.Values)
        {
            var serviceId = ElementIdentity(service);
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
            foreach (var inputDecisionId in DecisionServiceReferences(service, "inputDecision"))
                if (!_decisions.ContainsKey(inputDecisionId))
                    throw new InvalidOperationException(
                        $"Decision service '{serviceId}' references unknown input decision '{inputDecisionId}'.");
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

    private static bool IsBoxedExpression(XElement element) => element.Name.LocalName is
        "decisionTable" or "literalExpression" or "context" or "conditional" or "list"
        or "invocation" or "functionDefinition" or "relation" or "filter" or "for"
        or "some" or "every";

    private static XElement DecisionExpression(XElement decision) =>
        decision.Elements().Single(IsBoxedExpression);

    private string BoxedExpressionToFeel(XElement expression) => expression.Name.LocalName switch
    {
        "decisionTable" => DecisionTableToFeel(expression),
        "literalExpression" => RewriteKnowledgeReferences(RewriteExternalJavaFunctions(ExpressionText(expression))),
        "context" => ContextToFeel(expression),
        "conditional" => ConditionalToFeel(expression),
        "list" => $"[{string.Join(", ", expression.Elements().Where(IsBoxedExpression).Select(BoxedExpressionToFeel))}]",
        "invocation" => InvocationToFeel(expression),
        "functionDefinition" => FunctionDefinitionToFeel(expression),
        "relation" => RelationToFeel(expression),
        "filter" => FilterToFeel(expression),
        "for" => ForToFeel(expression),
        "some" or "every" => QuantifiedToFeel(expression),
        _ => throw new InvalidOperationException(
            $"Unsupported DMN boxed expression '{expression.Name.LocalName}'.")
    };

    private string DecisionTableToFeel(XElement table)
    {
        var inputExpressions = table.Elements(_namespace + "input")
            .Select(InputExpression)
            .ToArray();
        var outputs = table.Elements(_namespace + "output")
            .Select(output => (string?)output.Attribute("name")
                              ?? (string?)output.Attribute("label")
                              ?? RequiredAttribute(output, "id"))
            .ToArray();
        var rules = table.Elements(_namespace + "rule").Select(rule =>
        {
            var inputEntries = rule.Elements(_namespace + "inputEntry").ToArray();
            var conditions = inputExpressions.Select((input, index) =>
            {
                var unaryTests = ExpressionText(inputEntries[index], "-");
                return unaryTests == "-" ? "true" : $"({input}) in ({unaryTests})";
            });
            var outputEntries = rule.Elements(_namespace + "outputEntry").ToArray();
            var values = outputEntries.Select(entry => ExpressionText(entry, "null")).ToArray();
            var result = values.Length == 1
                ? values[0]
                : $"{{ {string.Join(", ", outputs.Zip(values, (name, value) => $"{name}: ({value})"))} }}";
            return new CompiledRule(string.Join(" and ", conditions.Select(condition => $"({condition})")), result);
        }).ToArray();

        var hitPolicy = ((string?)table.Attribute("hitPolicy") ?? "UNIQUE").Trim().ToUpperInvariant();
        if (hitPolicy is "PRIORITY" or "OUTPUT ORDER")
        {
            var priorities = table.Elements(_namespace + "output")
                .Select(output => (output.Element(_namespace + "outputValues")?.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToArray();
            rules = rules.OrderBy(rule =>
            {
                var rank = priorities.Length == 0 || priorities[0].Length == 0
                    ? 0
                    : Array.IndexOf(priorities[0], rule.Result.Trim());
                return rank < 0 ? int.MaxValue : rank;
            }).ToArray();
        }

        if (hitPolicy is "COLLECT" or "RULE ORDER" or "OUTPUT ORDER")
        {
            var matches = $"[{string.Join(", ", rules.Select(rule => $"if ({rule.Condition}) then ({rule.Result}) else null"))}][item != null]";
            var aggregation = ((string?)table.Attribute("aggregation"))?.Trim().ToUpperInvariant();
            var defaultOutput = table.Elements(_namespace + "output").SingleOrDefault()
                ?.Element(_namespace + "defaultOutputEntry");
            var aggregate = aggregation switch
            {
                "SUM" => $"sum(__vertexMatches)",
                "MIN" => $"min(__vertexMatches)",
                "MAX" => $"max(__vertexMatches)",
                "COUNT" => $"count(__vertexMatches)",
                _ => "__vertexMatches"
            };
            if (defaultOutput is not null)
            {
                var fallback = ExpressionText(defaultOutput, "null");
                return $"{{ __vertexMatches: ({matches}), __vertexCollectResult: "
                       + $"(if count(__vertexMatches) = 0 then ({fallback}) else ({aggregate})) }}.__vertexCollectResult";
            }
            return aggregation switch
            {
                "SUM" => $"sum({matches})",
                "MIN" => $"min({matches})",
                "MAX" => $"max({matches})",
                "COUNT" => $"count({matches})",
                _ => matches
            };
        }

        var resultExpression = "null";
        for (var index = rules.Length - 1; index >= 0; index--)
            resultExpression = $"if ({rules[index].Condition}) then ({rules[index].Result}) else ({resultExpression})";
        return resultExpression;
    }

    private string ContextToFeel(XElement context)
    {
        var entries = new List<string>();
        string? result = null;
        foreach (var entry in context.Elements(_namespace + "contextEntry"))
        {
            var expression = entry.Elements().FirstOrDefault(IsBoxedExpression)
                             ?? throw new InvalidOperationException("DMN contextEntry requires a boxed expression.");
            var value = BoxedExpressionToFeel(expression);
            var name = (string?)entry.Element(_namespace + "variable")?.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
                result = value;
            else
                entries.Add($"{ContextKeyToFeel(name)}: ({value})");
        }

        if (result is null) return $"{{ {string.Join(", ", entries)} }}";
        entries.Add($"__vertexContextResult: ({result})");
        return $"{{ {string.Join(", ", entries)} }}.__vertexContextResult";
    }

    private static string ContextKeyToFeel(string name) => JsonSerializer.Serialize(name);

    private string ConditionalToFeel(XElement conditional)
    {
        string Convert(string wrapperName)
        {
            var wrapper = conditional.Element(_namespace + wrapperName)
                          ?? throw new InvalidOperationException($"DMN conditional requires '{wrapperName}'.");
            var expression = wrapper.Elements().SingleOrDefault(IsBoxedExpression)
                             ?? throw new InvalidOperationException(
                                 $"DMN conditional '{wrapperName}' requires one boxed expression.");
            return BoxedExpressionToFeel(expression);
        }

        return $"if ({Convert("if")}) then ({Convert("then")}) else ({Convert("else")})";
    }

    private string InvocationToFeel(XElement invocation)
    {
        var calledFunction = invocation.Elements().FirstOrDefault(IsBoxedExpression)
                             ?? throw new InvalidOperationException("DMN invocation requires a called function expression.");
        var bindings = invocation.Elements(_namespace + "binding").Select(binding =>
        {
            var value = binding.Elements().FirstOrDefault(IsBoxedExpression)
                        ?? throw new InvalidOperationException("DMN invocation binding requires a boxed expression.");
            var parameter = binding.Element(_namespace + "parameter")
                            ?? throw new InvalidOperationException("DMN invocation binding requires a parameter.");
            return new InvocationBinding(RequiredAttribute(parameter, "name"), BoxedExpressionToFeel(value));
        }).ToArray();
        var calledExpression = BoxedExpressionToFeel(calledFunction);
        IEnumerable<string> arguments = bindings.Select(binding =>
            $"{binding.Name}: ({binding.Expression})");
        if (calledFunction.Name.LocalName == "literalExpression")
        {
            var knowledge = _businessKnowledgeModels
                .FirstOrDefault(entry =>
                    string.Equals(KnowledgeBindingName(entry.Value), calledExpression, StringComparison.Ordinal)
                    || string.Equals(KnowledgeAlias(entry.Key), calledExpression, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(knowledge.Key))
            {
                calledExpression = KnowledgeAlias(knowledge.Key);
                var byName = bindings.ToDictionary(binding => binding.Name, StringComparer.Ordinal);
                arguments = knowledge.Value.Element(_namespace + "encapsulatedLogic")!
                    .Elements(_namespace + "formalParameter")
                    .Select(parameter =>
                    {
                        var name = RequiredAttribute(parameter, "name");
                        return byName.TryGetValue(name, out var binding)
                            ? $"({binding.Expression})"
                            : "__vertexMissingArgument()";
                    });
            }
        }
        return $"({calledExpression})({string.Join(", ", arguments)})";
    }

    private string FunctionDefinitionToFeel(XElement function)
    {
        var parameters = function.Elements(_namespace + "formalParameter").ToArray();
        var body = function.Elements().FirstOrDefault(IsBoxedExpression)
                   ?? throw new InvalidOperationException("DMN functionDefinition requires a body expression.");
        if (string.Equals((string?)function.Attribute("kind"), "Java", StringComparison.OrdinalIgnoreCase))
            return ExternalJavaFunctionToFeel(parameters, body);
        var bodyExpression = CoerceFeelExpression(BoxedExpressionToFeel(body), TypeReference(body));
        return $"function({string.Join(", ", parameters.Select(FormalParameterToFeel))}) ({bodyExpression})";
    }

    private string ExternalJavaFunctionToFeel(IReadOnlyList<XElement> parameters, XElement body)
    {
        if (body.Name.LocalName != "context")
            throw new InvalidOperationException("A Java external function requires a context body.");
        var metadata = body.Elements(_namespace + "contextEntry").ToDictionary(
            entry => RequiredAttribute(
                entry.Element(_namespace + "variable")
                ?? throw new InvalidOperationException("External function metadata requires a variable."),
                "name"),
            entry => JsonSerializer.Deserialize<string>(ExpressionText(
                entry.Elements().Single(IsBoxedExpression)))
                     ?? throw new InvalidOperationException("External function metadata must be a string."),
            StringComparer.Ordinal);
        if (!metadata.TryGetValue("class", out var className)
            || !metadata.TryGetValue("method signature", out var signature))
            throw new InvalidOperationException("A Java external function requires class and method signature metadata.");
        var names = parameters.Select(parameter => RequiredAttribute(parameter, "name")).ToArray();
        return $"function({string.Join(", ", parameters.Select(FormalParameterToFeel))}) "
               + $"(__vertexJavaExternal({JsonSerializer.Serialize(className)}, "
               + $"{JsonSerializer.Serialize(signature)}, [{string.Join(", ", names)}]))";
    }

    private static string RewriteExternalJavaFunctions(string expression)
    {
        const string pattern = """function\s*\((?<parameters>[^)]*)\)\s*external\s*\{\s*java\s*:\s*\{\s*class\s*:\s*(?<class>"(?:\\.|[^"])*")\s*,\s*method\s+signature\s*:\s*(?<signature>"(?:\\.|[^"])*")\s*\}\s*\}""";
        return Regex.Replace(expression, pattern, match =>
        {
            var parameters = match.Groups["parameters"].Value;
            var arguments = parameters.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(parameter => parameter.Split(':', 2)[0].Trim());
            return $"function({parameters}) (__vertexJavaExternal({match.Groups["class"].Value}, "
                   + $"{match.Groups["signature"].Value}, [{string.Join(", ", arguments)}]))";
        }, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private string FormalParameterToFeel(XElement parameter)
    {
        var name = RequiredAttribute(parameter, "name");
        var type = TypeReference(parameter);
        return string.IsNullOrWhiteSpace(type)
            ? name
            : $"{name}: {ResolveFeelType(type, new HashSet<string>(StringComparer.Ordinal))}";
    }

    private string RelationToFeel(XElement relation)
    {
        var columns = relation.Elements(_namespace + "column")
            .Select(column => RequiredAttribute(column, "name"))
            .ToArray();
        var rows = relation.Elements(_namespace + "row").Select(row =>
        {
            var values = row.Elements().Where(IsBoxedExpression).ToArray();
            if (values.Length != columns.Length)
                throw new InvalidOperationException("Every DMN relation row must match the declared column count.");
            var entries = columns.Zip(values, (column, value) =>
                $"{column}: ({BoxedExpressionToFeel(value)})");
            return $"{{ {string.Join(", ", entries)} }}";
        });
        return $"[{string.Join(", ", rows)}]";
    }

    private string FilterToFeel(XElement filter) =>
        $"({WrappedExpression(filter, "in")})[{WrappedExpression(filter, "match")}]";

    private string ForToFeel(XElement forExpression)
    {
        var iterator = RequiredAttribute(forExpression, "iteratorVariable");
        return $"for {iterator} in ({WrappedExpression(forExpression, "in")}) "
               + $"return ({WrappedExpression(forExpression, "return")})";
    }

    private string QuantifiedToFeel(XElement quantified)
    {
        var iterator = RequiredAttribute(quantified, "iteratorVariable");
        return $"{quantified.Name.LocalName} {iterator} in ({WrappedExpression(quantified, "in")}) "
               + $"satisfies ({WrappedExpression(quantified, "satisfies")})";
    }

    private string WrappedExpression(XElement parent, string wrapperName)
    {
        var wrapper = parent.Element(_namespace + wrapperName)
                      ?? throw new InvalidOperationException(
                          $"DMN {parent.Name.LocalName} requires '{wrapperName}'.");
        var expression = wrapper.Elements().SingleOrDefault(IsBoxedExpression)
                         ?? throw new InvalidOperationException(
                             $"DMN {parent.Name.LocalName}/{wrapperName} requires one boxed expression.");
        return BoxedExpressionToFeel(expression);
    }

    private IReadOnlyDictionary<string, XElement> ElementsById(string localName) =>
        _root.Elements(_namespace + localName)
            .ToDictionary(ElementIdentity, StringComparer.Ordinal);

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

    private IEnumerable<string> RequiredKnowledge(XElement decision) =>
        decision.Elements(_namespace + "knowledgeRequirement")
            .Select(requirement => requirement.Element(_namespace + "requiredKnowledge"))
            .Where(required => required is not null)
            .Select(required => LocalReference(required!, "href"));

    private string CompileDecisionExpression(
        XElement decision,
        XElement expression,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var feel = BoxedExpressionToFeel(expression);
        feel = RewriteDecisionReferences(feel, RequiredDecisions(decision));
        var outputType = TypeReference(expression)
                         ?? TypeReference(decision.Element(_namespace + "variable"));
        var mismatchExpression = expression.Name.LocalName == "literalExpression"
                                 && ExpressionText(expression).TrimStart().StartsWith("context(", StringComparison.Ordinal)
                                 && !string.IsNullOrWhiteSpace(outputType)
                                 && _itemDefinitions.TryGetValue(outputType.Split(':').Last(), out var outputDefinition)
                                 && outputDefinition.Elements(_namespace + "itemComponent").Any()
            ? "null"
            : "__vertexTypeError()";
        if (RequiresManagedStructuralType(outputType))
            return CompileDecisionExpression(decision, feel, suppliedDecisions);
        return CompileDecisionExpression(
            decision,
            CoerceFeelExpression(feel, outputType, mismatchExpression),
            suppliedDecisions);
    }

    private string CompileDecisionExpression(
        XElement decision,
        string expression,
        IReadOnlySet<string>? suppliedDecisions)
    {
        expression = BindRequiredDecisions(decision, expression, suppliedDecisions);
        expression = BindRequiredKnowledge(decision, expression, suppliedDecisions);
        return ResolveItemDefinitionTypes(expression);
    }

    private string CoerceFeelExpression(
        string expression,
        string? typeRef,
        string mismatchExpression = "__vertexTypeError()")
    {
        if (string.IsNullOrWhiteSpace(typeRef)) return expression;
        var type = typeRef.Split(':').Last().Trim();
        var resolvedType = ResolveFeelType(type, new HashSet<string>(StringComparer.Ordinal));
        var value = "__vertexCoerceValue";
        string coercion;
        if (_itemDefinitions.TryGetValue(type, out var definition)
            && (bool?)definition.Attribute("isCollection") == true)
        {
            var elementType = ResolveFeelType(TypeReference(definition) ?? "Any", new HashSet<string>(StringComparer.Ordinal));
            coercion = $"if ({value} instance of {resolvedType}) then {value} "
                       + $"else if ({value} instance of {elementType}) then [{value}] "
                       + $"else {mismatchExpression}";
        }
        else
        {
            coercion = $"if ({value} instance of {resolvedType}) then {value} "
                       + $"else if ({value} instance of list<any> and count({value}) = 1 "
                       + $"and {value}[1] instance of {resolvedType}) then {value}[1] "
                       + $"else {mismatchExpression}";
        }

        return $"{{ {value}: ({expression}), __vertexCoerceResult: "
               + $"(if {value} = null then null else {coercion}) }}.__vertexCoerceResult";
    }

    private bool RequiresManagedStructuralType(string? typeRef)
    {
        if (string.IsNullOrWhiteSpace(typeRef)) return false;
        var type = typeRef.Split(':').Last().Trim();
        return _itemDefinitions.TryGetValue(type, out var definition)
               && definition.Elements(_namespace + "itemComponent").Any();
    }

    private bool ValueMatchesType(object? value, string? typeRef)
    {
        if (value is null || string.IsNullOrWhiteSpace(typeRef)) return true;
        var type = typeRef.Split(':').Last().Trim();
        if (_itemDefinitions.TryGetValue(type, out var definition))
        {
            if ((bool?)definition.Attribute("isCollection") == true)
                return value is IEnumerable collection and not string and not IDictionary
                       && collection.Cast<object?>().All(item => ValueMatchesType(item, TypeReference(definition)));

            var components = definition.Elements(_namespace + "itemComponent").ToArray();
            if (components.Length > 0)
            {
                if (value is not IDictionary dictionary) return false;
                foreach (var component in components)
                {
                    var name = RequiredAttribute(component, "name");
                    if (!dictionary.Contains(name)) return false;
                    var componentValue = dictionary[name];
                    var componentType = TypeReference(component);
                    if ((bool?)component.Attribute("isCollection") == true)
                    {
                        if (componentValue is not IEnumerable items
                            || componentValue is string or IDictionary
                            || !items.Cast<object?>().All(item => ValueMatchesType(item, componentType)))
                            return false;
                    }
                    else if (!ValueMatchesType(componentValue, componentType))
                    {
                        return false;
                    }
                }
                return true;
            }
            return ValueMatchesType(value, TypeReference(definition));
        }

        return type.ToLowerInvariant() switch
        {
            "any" => true,
            "string" => value is string,
            "boolean" => value is bool,
            "number" => value is byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal,
            "date" => value is FeelTemporalValue { Kind: "date" },
            "time" => value is FeelTemporalValue { Kind: "time" },
            "datetime" or "date and time" => value is FeelTemporalValue { Kind: "date time" },
            "daytimeduration" or "days and time duration" or "yearmonthduration"
                or "years and months duration" or "duration" => value is FeelTemporalValue { Kind: "duration" },
            "context" => value is IDictionary,
            _ => true
        };
    }

    private string BindRequiredDecisions(
        XElement decision,
        string expression,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var dependencies = DecisionDependencyClosure(decision, suppliedDecisions).ToArray();
        if (dependencies.Length == 0) return expression;
        var bindings = dependencies.Select(id =>
            $"{DecisionAlias(id)}: ({RewriteDecisionReferences(
                BoxedExpressionToFeel(DecisionExpression(_decisions[id])), RequiredDecisions(_decisions[id]))})");
        return $"{{ {string.Join(", ", bindings)}, __vertexResult: ({expression}) }}.__vertexResult";
    }

    private IEnumerable<string> DecisionDependencyClosure(
        XElement decision,
        IReadOnlySet<string>? suppliedDecisions)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string id)
        {
            if (suppliedDecisions?.Contains(id) == true || !visited.Add(id)) return;
            foreach (var dependency in RequiredDecisions(_decisions[id])) Visit(dependency);
            ordered.Add(id);
        }

        foreach (var dependency in RequiredDecisions(decision)) Visit(dependency);
        return ordered;
    }

    private string BindRequiredKnowledge(
        XElement decision,
        string expression,
        IReadOnlySet<string>? suppliedDecisions = null)
    {
        var knowledgeRoots = RequiredKnowledge(decision)
            .Concat(DecisionDependencyClosure(decision, suppliedDecisions)
                .SelectMany(id => RequiredKnowledge(_decisions[id])));
        var requiredKnowledge = ExpandRequiredKnowledge(knowledgeRoots);
        if (requiredKnowledge.Length == 0) return expression;

        var bindings = requiredKnowledge.Select(id =>
        {
            if (_decisionServices.TryGetValue(id, out var service))
                return $"{KnowledgeAlias(id)}: {DecisionServiceToFeel(service)}";

            var model = _businessKnowledgeModels[id];
            var logic = model.Element(_namespace + "encapsulatedLogic")!;
            var parameters = logic.Elements(_namespace + "formalParameter").ToArray();
            var expression = logic.Elements().Single(IsBoxedExpression);
            var body = CoerceFeelExpression(BoxedExpressionToFeel(expression), TypeReference(expression));
            return $"{KnowledgeAlias(id)}: function("
                   + $"{string.Join(", ", parameters.Select(FormalParameterToFeel))}) ({body})";
        });

        return $"{{ {string.Join(", ", bindings)}, __vertexResult: ({expression}) }}.__vertexResult";
    }

    private string[] ExpandRequiredKnowledge(IEnumerable<string> roots)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string id)
        {
            if (!visited.Add(id)) return;
            if (_businessKnowledgeModels.TryGetValue(id, out var model))
            {
                foreach (var dependency in RequiredKnowledge(model)) Visit(dependency);
            }
            else if (_decisionServices.TryGetValue(id, out var service))
            {
                foreach (var decisionId in DecisionServiceDecisionClosure(service))
                    foreach (var dependency in RequiredKnowledge(_decisions[decisionId]))
                        Visit(dependency);
            }
            else
            {
                throw new InvalidOperationException($"Unknown required knowledge '{id}'.");
            }
            ordered.Add(id);
        }

        foreach (var root in roots) Visit(root);
        return ordered.ToArray();
    }

    private string KnowledgeAlias(string id) => _businessKnowledgeModels.ContainsKey(id)
        ? $"__vertexBkm{_businessKnowledgeModels.Keys.ToList().IndexOf(id)}"
        : $"__vertexDs{_decisionServices.Keys.ToList().IndexOf(id)}";

    private string DecisionAlias(string id) =>
        $"__vertexDecision{_decisions.Keys.ToList().IndexOf(id)}";

    private string RewriteDecisionReferences(string expression, IEnumerable<string> decisionIds)
    {
        foreach (var decision in decisionIds
                     .Distinct(StringComparer.Ordinal)
                     .Select(id => new KeyValuePair<string, XElement>(id, _decisions[id]))
                     .OrderByDescending(entry => DecisionBindingName(entry.Value).Length))
        {
            var name = DecisionBindingName(decision.Value);
            expression = RewriteOutsideFeelStrings(expression, segment => Regex.Replace(
                    segment,
                    $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?![A-Za-z0-9_])",
                    DecisionAlias(decision.Key),
                    RegexOptions.CultureInvariant));
        }
        return expression;
    }

    private static string RewriteOutsideFeelStrings(string expression, Func<string, string> rewrite)
    {
        var result = new System.Text.StringBuilder(expression.Length);
        var segmentStart = 0;
        for (var index = 0; index < expression.Length; index++)
        {
            if (expression[index] != '"') continue;
            result.Append(rewrite(expression[segmentStart..index]));
            var stringStart = index++;
            while (index < expression.Length)
            {
                if (expression[index] == '\\')
                {
                    index++;
                    continue;
                }
                if (expression[index] == '"') break;
                index++;
            }
            result.Append(expression[stringStart..Math.Min(index + 1, expression.Length)]);
            segmentStart = Math.Min(index + 1, expression.Length);
        }
        result.Append(rewrite(expression[segmentStart..]));
        return result.ToString();
    }

    private string KnowledgeBindingName(XElement model) =>
        (string?)model.Attribute("name")
        ?? (string?)model.Element(_namespace + "variable")?.Attribute("name")
        ?? RequiredAttribute(model, "id");

    private string RewriteKnowledgeReferences(string expression)
    {
        foreach (var model in _businessKnowledgeModels)
        {
            var name = KnowledgeBindingName(model.Value);
            expression = Regex.Replace(
                expression,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?=\s*\()",
                KnowledgeAlias(model.Key),
                RegexOptions.CultureInvariant);
            expression = Regex.Replace(
                expression,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?=\s*(?:,|\)|$))",
                KnowledgeAlias(model.Key),
                RegexOptions.CultureInvariant);
        }
        foreach (var service in _decisionServices)
        {
            var name = KnowledgeBindingName(service.Value);
            expression = Regex.Replace(
                expression,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?=\s*\()",
                KnowledgeAlias(service.Key),
                RegexOptions.CultureInvariant);
            expression = Regex.Replace(
                expression,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?=\s*(?:,|\)|$))",
                KnowledgeAlias(service.Key),
                RegexOptions.CultureInvariant);
        }
        return expression;
    }

    private string DecisionServiceToFeel(XElement service)
    {
        var inputData = DecisionServiceReferences(service, "inputData").ToArray();
        var inputDecisions = DecisionServiceReferences(service, "inputDecision").ToArray();
        var parameterIds = inputData.Concat(inputDecisions).ToArray();
        var parameterNames = inputData
            .Select(id => DecisionBindingName(_inputData[id]))
            .Concat(inputDecisions.Select(id => DecisionBindingName(_decisions[id])))
            .ToArray();

        var closure = DecisionServiceDecisionClosure(service).ToArray();
        var entries = closure.Select(id =>
            $"{DecisionBindingName(_decisions[id])}: ({BoxedExpressionToFeel(DecisionExpression(_decisions[id]))})").ToList();
        var outputIds = DecisionServiceReferences(service, "outputDecision").ToArray();
        var result = outputIds.Length == 1
            ? DecisionBindingName(_decisions[outputIds[0]])
            : $"{{ {string.Join(", ", outputIds.Select(id =>
                $"{JsonSerializer.Serialize(DecisionBindingName(_decisions[id]))}: "
                + $"{DecisionBindingName(_decisions[id])}"))} }}";
        result = CoerceFeelExpression(result, DecisionServiceOutputType(service));
        entries.Add($"__vertexServiceResult: ({result})");
        var body = $"{{ {string.Join(", ", entries)} }}.__vertexServiceResult";

        var parameterBindings = parameterIds.Zip(parameterNames, (id, name) =>
        {
            var source = _inputData.TryGetValue(id, out var input) ? input : _decisions[id];
            var type = TypeReference(source.Element(_namespace + "variable"));
            return string.IsNullOrWhiteSpace(type) ? null : $"{name}: ({CoerceFeelExpression(name, type)})";
        }).Where(binding => binding is not null).ToArray();
        if (parameterBindings.Length > 0)
            body = $"{{ {string.Join(", ", parameterBindings)}, __vertexServiceBody: ({body}) }}.__vertexServiceBody";

        return $"function({string.Join(", ", parameterNames)}) ({body})";
    }

    private string? DecisionServiceOutputType(XElement service)
    {
        var serviceType = TypeReference(service.Element(_namespace + "variable"));
        if (string.IsNullOrWhiteSpace(serviceType)) return null;
        var type = serviceType.Split(':').Last();
        var function = _itemDefinitions.TryGetValue(type, out var definition)
            ? definition.Element(_namespace + "functionItem")
            : null;
        return (string?)function?.Attribute("outputTypeRef");
    }

    private IEnumerable<string> DecisionServiceDecisionClosure(XElement service)
    {
        var supplied = DecisionServiceReferences(service, "inputDecision").ToHashSet(StringComparer.Ordinal);
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string id)
        {
            if (supplied.Contains(id) || !visited.Add(id)) return;
            foreach (var dependency in RequiredDecisions(_decisions[id])) Visit(dependency);
            ordered.Add(id);
        }

        foreach (var output in DecisionServiceReferences(service, "outputDecision")) Visit(output);
        foreach (var encapsulated in DecisionServiceReferences(service, "encapsulatedDecision")) Visit(encapsulated);
        return ordered;
    }

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

    private void AddToContext(
        XElement decision,
        DecisionEvaluation evaluation,
        IDictionary<string, object> context)
    {
        foreach (var output in evaluation.Variables) context[output.Key] = output.Value;
        context[DecisionBindingName(decision)] = evaluation.Value!;
        context[DecisionAlias(ElementIdentity(decision))] = evaluation.Value!;
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

    private string LocalReference(XElement element, string attribute)
    {
        var reference = RequiredAttribute(element, attribute);
        if (reference.StartsWith('#')) return Uri.UnescapeDataString(reference[1..]);

        var fragmentIndex = reference.LastIndexOf('#');
        var modelNamespace = (string?)_root.Attribute("namespace");
        if (fragmentIndex > 0
            && string.Equals(reference[..fragmentIndex], modelNamespace, StringComparison.Ordinal))
            return Uri.UnescapeDataString(reference[(fragmentIndex + 1)..]);

        throw new InvalidOperationException($"DMN reference '{reference}' does not address the local model namespace.");
    }

    private static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"DMN {element.Name.LocalName} requires attribute '{name}'.");

    private static string ElementIdentity(XElement element) =>
        (string?)element.Attribute("id") is { Length: > 0 } id
            ? id
            : (string?)element.Attribute("name") is { Length: > 0 } name
                ? name
                : throw new InvalidOperationException(
                    $"DMN {element.Name.LocalName} requires an 'id' or 'name' attribute.");

    private static decimal ToDecimal(object value)
    {
        if (value is decimal decimalValue) return decimalValue;
        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDecimal(CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                // Fall through to the standard domain error below.
            }
        }

        throw new InvalidOperationException($"COLLECT aggregation requires numeric values, got '{value}'.");
    }

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
    private sealed record CompiledRule(string Condition, string Result);
    private sealed record InvocationBinding(string Name, string Expression);
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
