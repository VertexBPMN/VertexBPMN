using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Engine.Execution;

public sealed partial class PersistentProcessExecutionRuntime
{
    private static Dictionary<string, object> FreezeTaskFormVariables(string? schema,
        IDictionary<string, object> instanceVariables, IReadOnlyDictionary<string, object>? localVariables)
    {
        var snapshot = localVariables is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(localVariables, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(schema)) { return snapshot; }
        snapshot = snapshot.Where(pair => pair.Key.StartsWith("$vertex.", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        using var document = JsonDocument.Parse(schema);
        if (document.RootElement.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
        {
            CollectTaskFormPrefill(components, CreateActivityVariables(instanceVariables, localVariables), snapshot);
        }
        return snapshot;
    }

    private static void CollectTaskFormPrefill(JsonElement components, IReadOnlyDictionary<string, object> variables,
        Dictionary<string, object> snapshot)
    {
        foreach (var component in components.EnumerateArray())
        {
            if (component.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                && variables.TryGetValue(key.GetString()!, out var value))
            {
                snapshot[key.GetString()!] = JsonSerializer.SerializeToElement(value).Clone();
            }
            if (component.TryGetProperty("components", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                CollectTaskFormPrefill(children, variables, snapshot);
            }
        }
    }

    private async Task<Dictionary<string, string>> ReadTaskFormBindingAsync(ProcessInstance instance, ExecutionNode node,
        CancellationToken cancellationToken)
    {
        // The execution parser aliases legacy formKey to formRef. Recover the original
        // binding kind here so only explicit external formKey resources bypass managed revisions.
        var definition = instance.ProcessDefinition ?? await _db.ProcessDefinitions.FindAsync([instance.ProcessDefinitionId], cancellationToken)
            ?? throw new InvalidOperationException("Process definition could not be resolved.");
        var task = XDocument.Parse(definition.BpmnXml).Descendants().First(element =>
            element.Name.LocalName == "userTask" && (string?)element.Attribute("id") == node.Id);
        var form = task.Descendants().FirstOrDefault(element => element.Name.LocalName == "form");
        var reference = form?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "formRef")
            ?? task.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "formRef");
        var key = form?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "formKey")
            ?? task.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "formKey");
        var attributes = new Dictionary<string, string>(node.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("formRef");
        attributes.Remove("formKey");
        if (reference is not null) { attributes["formRef"] = reference.Value; }
        else if (key is not null) { attributes["formKey"] = key.Value; }
        return attributes;
    }

    private async Task<FormDefinitionRevisionRecord?> ResolveTaskFormRevisionAsync(string? tenantId, string? key,
        Dictionary<string, string> attributes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (attributes.ContainsKey("formRef")) { throw new InvalidOperationException("Form reference must not be empty."); }
            return null;
        }
        var form = await _db.FormDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && (x.Key == key || x.Id == key), cancellationToken);
        var explicitVersion = attributes.GetValueOrDefault("formRefVersion");
        var binding = attributes.GetValueOrDefault("formRefBinding");
        if (binding is not null && binding != "latest" && binding != "version")
        { throw new InvalidOperationException($"Form binding '{binding}' cannot be resolved without a release contract."); }
        if (binding == "version" && explicitVersion is null)
        { throw new InvalidOperationException("Version-bound forms require formRefVersion."); }
        var version = form?.Version ?? 0;
        if (explicitVersion is not null && (!int.TryParse(explicitVersion, NumberStyles.None, CultureInfo.InvariantCulture, out version) || version < 1))
        { throw new InvalidOperationException($"Form '{key}' version '{explicitVersion}' could not be resolved."); }
        if (form is null)
        {
            // Camunda 7 external-resource formKey is intentionally schema-less. A plain
            // key or any formRef (even one with a resource prefix) must resolve locally.
            if (explicitVersion is null && !attributes.ContainsKey("formRef") && binding is null
                && (key.StartsWith("app:", StringComparison.Ordinal) || key.StartsWith("embedded:", StringComparison.Ordinal))) { return null; }
            throw new InvalidOperationException($"Form '{key}' version '{explicitVersion}' could not be resolved.");
        }
        var revision = await _db.FormDefinitionRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.FormDefinitionId == form.Id && x.Version == version, cancellationToken);
        return revision ?? throw new InvalidOperationException($"Form '{key}' version '{version}' could not be resolved.");
    }
}
