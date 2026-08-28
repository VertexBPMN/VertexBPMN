using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Application;

return DmnTckProgram.Run(args);

internal static class DmnTckProgram
{
    private static readonly XNamespace TestNamespace = "http://www.omg.org/spec/DMN/20160719/testcase";
    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    public static int Run(string[] args)
    {
        if (args.Length == 0 || !Directory.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/VertexBPMN.DmnTckRunner -- <DMN-TCK TestCases directory> [compliance-level-2|compliance-level-3] [test-group]");
            return 2;
        }

        var testCasesRoot = Path.GetFullPath(args[0]);
        var levels = args.Length > 1
            ? new[] { args[1] }
            : new[] { "compliance-level-2", "compliance-level-3" };
        var groupFilter = args.Length > 2 ? args[2] : null;
        var failures = new List<TckFailure>();
        var total = 0;
        var succeeded = 0;

        foreach (var level in levels)
        {
            var levelDirectory = Path.Combine(testCasesRoot, level);
            if (!Directory.Exists(levelDirectory))
            {
                Console.Error.WriteLine($"DMN-TCK level directory does not exist: {levelDirectory}");
                return 2;
            }

            var definitions = Directory.EnumerateFiles(
                         levelDirectory,
                         "*-test-*.xml",
                         SearchOption.AllDirectories)
                .Where(path => groupFilter is null
                               || string.Equals(
                                   Path.GetFileName(Path.GetDirectoryName(path)),
                                   groupFilter,
                                   StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
            foreach (var testDefinitionPath in definitions)
            {
                RunDefinition(testDefinitionPath, failures, ref total, ref succeeded);
                Console.Error.WriteLine(
                    $"[{level}] {Path.GetFileName(Path.GetDirectoryName(testDefinitionPath))}: "
                    + $"{succeeded}/{total} passed, {failures.Count} failed");
            }
        }

        var report = new
        {
            tckRoot = testCasesRoot,
            levels,
            groupFilter,
            total,
            succeeded,
            failed = failures.Count,
            failures
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }

    private static void RunDefinition(
        string testDefinitionPath,
        ICollection<TckFailure> failures,
        ref int total,
        ref int succeeded)
    {
        XDocument tests;
        string dmnXml;
        XDocument dmn;
        try
        {
            tests = XDocument.Load(testDefinitionPath, LoadOptions.SetLineInfo);
            var modelName = tests.Root?.Element(TestNamespace + "modelName")?.Value;
            if (string.IsNullOrWhiteSpace(modelName))
                throw new InvalidOperationException("testCases/modelName is missing.");
            var modelPath = Path.Combine(Path.GetDirectoryName(testDefinitionPath)!, modelName);
            dmnXml = File.ReadAllText(modelPath);
            dmn = XDocument.Parse(dmnXml, LoadOptions.SetLineInfo);
        }
        catch (Exception exception)
        {
            total++;
            failures.Add(new TckFailure(RelativeId(testDefinitionPath), "<definition>", exception.Message));
            return;
        }

        DmnDecisionGraph? graph = null;
        foreach (var testCase in tests.Root!.Elements(TestNamespace + "testCase"))
        {
            var caseId = (string?)testCase.Attribute("id") ?? "<unnamed>";
            total++;
            try
            {
                ExecuteCase(testCase, dmn.Root!, dmnXml, ref graph);
                succeeded++;
            }
            catch (Exception exception)
            {
                failures.Add(new TckFailure(RelativeId(testDefinitionPath), caseId, exception.Message));
            }
        }
    }

    private static void ExecuteCase(
        XElement testCase,
        XElement definitions,
        string dmnXml,
        ref DmnDecisionGraph? graph)
    {
        var inputs = testCase.Elements(TestNamespace + "inputNode")
            .ToDictionary(
                node => RequiredAttribute(node, "name"),
                node => ReadValue(node)!,
                StringComparer.Ordinal);
        var resultNodes = testCase.Elements(TestNamespace + "resultNode").ToArray();
        if (resultNodes.Length == 0)
            throw new InvalidOperationException("The TCK case contains no resultNode.");

        var caseType = ((string?)testCase.Attribute("type") ?? "decision").Trim();
        var invocableName = (string?)testCase.Attribute("invocableName");
        foreach (var resultNode in resultNodes)
        {
            var resultName = RequiredAttribute(resultNode, "name");
            var expectsError = (bool?)resultNode.Attribute("errorResult") == true;
            try
            {
                var targetName = caseType == "decisionService" && !string.IsNullOrWhiteSpace(invocableName)
                    ? invocableName
                    : resultName;
                var targetId = ResolveTargetId(definitions, caseType, targetName);
                graph ??= DmnDecisionGraph.Parse(dmnXml, targetId);
                var actualVariables = graph.Evaluate(targetId, inputs);
                if (expectsError)
                    throw new TckAssertionException($"Expected an error result for '{resultName}', but evaluation succeeded.");

                var expectedElement = resultNode.Element(TestNamespace + "expected")
                    ?? throw new TckAssertionException($"Expected value for '{resultName}' is missing.");
                var expected = ReadValue(expectedElement);
                var actual = ResolveActual(actualVariables, resultName, expected);
                if (!StructuralEquals(expected, actual))
                    throw new TckAssertionException(
                        $"Result '{resultName}' differs. Expected {Format(expected)}, actual {Format(actual)}.");
            }
            catch (TckAssertionException)
            {
                throw;
            }
            catch when (expectsError)
            {
                // The TCK explicitly expects evaluation to fail for this result.
            }
        }
    }

    private static string ResolveTargetId(XElement definitions, string caseType, string targetName)
    {
        var localName = caseType == "decisionService" ? "decisionService" : "decision";
        var target = definitions.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == localName
                && ((string?)element.Attribute("id") == targetName
                    || (string?)element.Attribute("name") == targetName));
        return (string?)target?.Attribute("id")
               ?? throw new InvalidOperationException($"DMN {localName} '{targetName}' was not found.");
    }

    private static object? ResolveActual(
        IReadOnlyDictionary<string, object> actualVariables,
        string resultName,
        object? expected)
    {
        if (actualVariables.TryGetValue(resultName, out var value)) return value;
        if (expected is IDictionary) return actualVariables;
        if (expected is IEnumerable and not string
            && actualVariables.Count > 1
            && actualVariables.Values.All(candidate => candidate is IEnumerable and not string))
        {
            var columns = actualVariables.ToDictionary(
                entry => entry.Key,
                entry => ((IEnumerable)entry.Value).Cast<object?>().ToArray(),
                StringComparer.Ordinal);
            var rowCount = columns.Values.Select(column => column.Length).Distinct().Single();
            return Enumerable.Range(0, rowCount)
                .Select(index => (object)columns.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value[index],
                    StringComparer.Ordinal))
                .ToList();
        }
        if (actualVariables.Count == 1) return actualVariables.Values.Single();
        throw new TckAssertionException(
            $"Result '{resultName}' is absent. Returned keys: {string.Join(", ", actualVariables.Keys)}.");
    }

    private static object? ReadValue(XElement container)
    {
        var value = container.Element(TestNamespace + "value");
        if (value is not null)
        {
            if ((bool?)value.Attribute(XsiNamespace + "nil") == true) return null;
            var type = ((string?)value.Attribute(XsiNamespace + "type"))?.Split(':').LastOrDefault();
            return ParseScalar(value.Value, type);
        }

        var list = container.Element(TestNamespace + "list");
        if (list is not null)
        {
            if ((bool?)list.Attribute(XsiNamespace + "nil") == true) return null;
            return list.Elements(TestNamespace + "item").Select(ReadValue).ToList();
        }

        var components = container.Elements(TestNamespace + "component").ToArray();
        if (components.Length > 0)
            return components.ToDictionary(
                component => AttributeValue(component, "name"),
                ReadValue,
                StringComparer.Ordinal);

        return null;
    }

    private static object ParseScalar(string text, string? type) => type switch
    {
        "boolean" => XmlConvert.ToBoolean(text),
        "byte" or "short" or "int" or "integer" or "long" or "nonNegativeInteger" or "positiveInteger"
            or "nonPositiveInteger" or "negativeInteger" or "unsignedByte" or "unsignedShort" or "unsignedInt"
            or "unsignedLong" => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture),
        "decimal" or "double" or "float" => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
        _ => text
    };

    private static bool StructuralEquals(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected is null || actual is null) return false;
        if (expected is IDictionary expectedDictionary && actual is IDictionary actualDictionary)
        {
            if (expectedDictionary.Count != actualDictionary.Count) return false;
            foreach (var key in expectedDictionary.Keys)
                if (!actualDictionary.Contains(key)
                    || !StructuralEquals(expectedDictionary[key], actualDictionary[key]))
                    return false;
            return true;
        }

        if (expected is IEnumerable expectedSequence and not string
            && actual is IEnumerable actualSequence and not string)
            return expectedSequence.Cast<object?>().SequenceEqual(
                actualSequence.Cast<object?>(), StructuralComparer.Instance);
        if (TryGetDecimal(expected, out var expectedNumber)
            && TryGetDecimal(actual, out var actualNumber))
            return Math.Abs(expectedNumber - actualNumber) < 0.00000001m;
        if (expected is string expectedText && actual is string actualText
            && DateTimeOffset.TryParse(expectedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expectedDateTime)
            && DateTimeOffset.TryParse(actualText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var actualDateTime))
            return expectedDateTime.EqualsExact(actualDateTime);
        return string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal);
    }

    private static bool TryGetDecimal(object value, out decimal number)
    {
        if (value is decimal decimalValue)
        {
            number = decimalValue;
            return true;
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double)
        {
            number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }

        return decimal.TryParse(
            value as string,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static string RequiredAttribute(XElement element, string name) =>
        (string?)element.Attribute(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{element.Name.LocalName}/@{name} is missing.");

    private static string AttributeValue(XElement element, string name) =>
        (string?)element.Attribute(name)
        ?? throw new InvalidOperationException($"{element.Name.LocalName}/@{name} is missing.");

    private static string Format(object? value) => JsonSerializer.Serialize(value);
    private static string RelativeId(string path) => Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path);

    private sealed record TckFailure(string Definition, string TestCase, string Message);
    private sealed class TckAssertionException(string message) : Exception(message);

    private sealed class StructuralComparer : IEqualityComparer<object?>
    {
        public static StructuralComparer Instance { get; } = new();
        public new bool Equals(object? left, object? right) => StructuralEquals(left, right);
        public int GetHashCode(object? value) => value?.GetHashCode() ?? 0;
    }
}
