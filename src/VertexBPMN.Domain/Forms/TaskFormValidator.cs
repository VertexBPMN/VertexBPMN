using System.Text.Json;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Forms;

/// <summary>Validates completion against the task's frozen schema, never the latest form.</summary>
public static class TaskFormValidator
{
    public static UserTaskValidationResult Validate(UserTask task, IDictionary<string, object>? values)
    {
        var errors = new List<string>();
        var data = values ?? new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(task.FormSchema))
        {
            CheckRequiredFields(task.RequiredFields, data, errors);
            return errors.Count == 0 ? UserTaskValidationResult.Success() : UserTaskValidationResult.Failure(errors);
        }
        try
        {
            using var schema = JsonDocument.Parse(task.FormSchema);
            if (!schema.RootElement.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
            {
                errors.Add("The task form schema has no components array.");
            }
            else
            {
                var fields = new Dictionary<string, (JsonElement Component, bool ReadOnly)>(StringComparer.Ordinal);
                CollectFields(components, fields, errors);
                var effectiveData = new Dictionary<string, object>(data, StringComparer.Ordinal);
                foreach (var (key, field) in fields.Where(pair => pair.Value.ReadOnly))
                {
                    effectiveData.Remove(key);
                    if (task.LocalVariables.TryGetValue(key, out var prefill))
                    {
                        effectiveData[key] = prefill;
                        CheckFieldValue(key, field.Component, prefill, errors);
                    }
                }
                CheckRequiredFields(task.RequiredFields, effectiveData, errors);
                CheckRequiredComponents(components, effectiveData, errors);
                foreach (var (key, value) in data)
                {
                    if (!fields.TryGetValue(key, out var field))
                    {
                        errors.Add($"Field '{key}' is not an allowed result variable.");
                        continue;
                    }
                    if (field.ReadOnly)
                    {
                        errors.Add($"Field '{key}' is read-only.");
                        continue;
                    }
                    CheckFieldValue(key, field.Component, value, errors);
                }
            }
        }
        catch (JsonException)
        {
            errors.Add("The task form schema is invalid JSON.");
        }
        catch (InvalidOperationException)
        {
            errors.Add("The task form schema has an invalid component structure.");
        }
        return errors.Count == 0 ? UserTaskValidationResult.Success() : UserTaskValidationResult.Failure(errors);
    }

    private static void CollectFields(JsonElement components, Dictionary<string, (JsonElement Component, bool ReadOnly)> fields,
        List<string> errors, bool inheritedReadOnly = false)
    {
        foreach (var component in components.EnumerateArray())
        {
            var type = component.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
            if (!IsSupportedInput(component) && type is not ("text" or "html" or "image" or "button" or "group" or "default"
                or "separator" or "spacer" or "table" or "iframe" or "documentPreview"))
            {
                errors.Add($"Form component type '{type ?? "<missing>"}' is not supported for task completion.");
            }
            var readOnly = inheritedReadOnly || IsReadOnly(component);
            if (component.TryGetProperty("components", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                if (type is "group" or "default")
                {
                    CollectFields(children, fields, errors, readOnly);
                }
                else
                {
                    errors.Add($"Form component type '{type}' cannot contain input components.");
                }
            }
            if (IsSupportedInput(component)
                && component.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                && !fields.TryAdd(key.GetString()!, (component, readOnly)))
            {
                errors.Add($"Field '{key.GetString()}' is declared more than once.");
            }
        }
    }

    private static bool IsSupportedInput(JsonElement component) =>
        component.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
        && type.GetString() is "number" or "checkbox" or "textfield" or "textarea" or "select" or "radio";

    private static bool IsReadOnly(JsonElement field) =>
        (field.TryGetProperty("readonly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True)
        || (field.TryGetProperty("readOnly", out var camelReadOnly) && camelReadOnly.ValueKind == JsonValueKind.True)
        || (field.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True);

    private static void CheckFieldValue(string key, JsonElement field, object? value, List<string> errors)
    {
        if (value is null)
        {
            return;
        }
        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);
        var type = field.TryGetProperty("type", out var kind) ? kind.GetString() : null;
        var correctType = type switch
        {
            "number" => element.ValueKind == JsonValueKind.Number,
            "checkbox" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "textfield" or "textarea" or "select" or "radio" => element.ValueKind == JsonValueKind.String,
            _ => true
        };
        if (!correctType)
        {
            errors.Add($"Field '{key}' has the wrong type.");
            return;
        }
        if (field.TryGetProperty("values", out var options) && options.ValueKind == JsonValueKind.Array
            && !options.EnumerateArray().Any(option => option.TryGetProperty("value", out var allowed)
                && string.Equals(allowed.GetRawText(), element.GetRawText(), StringComparison.Ordinal)))
        {
            errors.Add($"Field '{key}' has a value outside the allowed choices.");
        }
    }

    private static void CheckRequiredFields(IEnumerable<string> requiredFields, IDictionary<string, object> data, List<string> errors)
    {
        foreach (var key in requiredFields)
        {
            if (!data.TryGetValue(key, out var value) || value is null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                errors.Add($"Field '{key}' is required.");
            }
        }
    }

    private static void CheckRequiredComponents(JsonElement components, IDictionary<string, object> data, List<string> errors)
    {
        foreach (var component in components.EnumerateArray())
        {
            if (component.TryGetProperty("components", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                CheckRequiredComponents(children, data, errors);
            }
            if (!component.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            var key = keyElement.GetString()!;
            if (component.TryGetProperty("validate", out var validation) && validation.TryGetProperty("required", out var required)
                && required.ValueKind == JsonValueKind.True
                && (!data.TryGetValue(key, out var value) || value is null || string.IsNullOrWhiteSpace(value.ToString())))
            {
                errors.Add($"Field '{key}' is required.");
            }
        }
    }
}
