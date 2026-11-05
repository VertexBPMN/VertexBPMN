using System.Text.Json;

namespace VertexBPMN.Api.Services;

// Closed JSON-schema subset, not a general Draft implementation. Remote refs are never evaluated.
internal static class EndpointJsonSchema
{
    private static readonly HashSet<string> Keywords = new(["type", "properties", "required", "additionalProperties",
        "items", "maxItems", "maxLength", "minLength", "minimum", "maximum", "enum"], StringComparer.Ordinal);
    internal static bool Matches(JsonElement schema, JsonElement value, int depth = 0)
    {
        if (depth > 16 || schema.ValueKind != JsonValueKind.Object
            || schema.EnumerateObject().Any(x => !Keywords.Contains(x.Name))
            || !schema.TryGetProperty("type", out var type)) { return false; }
        if (schema.TryGetProperty("enum", out var options)
            && !options.EnumerateArray().Any(x => JsonElement.DeepEquals(x, value))) { return false; }
        return type.GetString() switch
        {
            "object" => ObjectMatches(schema, value, depth),
            "array" => value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items)
                && (!schema.TryGetProperty("maxItems", out var count) || value.GetArrayLength() <= count.GetInt32())
                && value.EnumerateArray().All(x => Matches(items, x, depth + 1)),
            "string" => value.ValueKind == JsonValueKind.String
                && (!schema.TryGetProperty("maxLength", out var max) || value.GetString()!.Length <= max.GetInt32())
                && (!schema.TryGetProperty("minLength", out var min) || value.GetString()!.Length >= min.GetInt32()),
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) && NumberMatches(schema, value),
            "number" => value.ValueKind == JsonValueKind.Number && NumberMatches(schema, value),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };
    }
    private static bool NumberMatches(JsonElement schema, JsonElement value) =>
        (!schema.TryGetProperty("minimum", out var min) || value.GetDecimal() >= min.GetDecimal())
        && (!schema.TryGetProperty("maximum", out var max) || value.GetDecimal() <= max.GetDecimal());
    private static bool ObjectMatches(JsonElement schema, JsonElement value, int depth)
    {
        if (value.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties)) { return false; }
        if (schema.TryGetProperty("required", out var required)
            && required.EnumerateArray().Any(x => !value.TryGetProperty(x.GetString()!, out _))) { return false; }
        return value.EnumerateObject().All(x => properties.TryGetProperty(x.Name, out var field) && Matches(field, x.Value, depth + 1));
    }
}
