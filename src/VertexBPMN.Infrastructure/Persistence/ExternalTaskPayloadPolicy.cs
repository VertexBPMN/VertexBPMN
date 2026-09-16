using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VertexBPMN.Infrastructure.Persistence;

internal static class ExternalTaskPayloadPolicy
{
    private const int MaximumBytes = 128 * 1024;
    private const int MaximumDepth = 32;

    public static (string CanonicalJson, string Hash) ValidateResult(JsonElement result, string schemaSnapshot)
    {
        ValidateJson(result, 1);
        if (Encoding.UTF8.GetByteCount(result.GetRawText()) > MaximumBytes)
            throw new ExternalTaskPayloadException("payload_too_large");
        ValidateOutputSchema(result, schemaSnapshot);
        var canonical = Canonicalize(result);
        return (canonical, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }

    public static string HashFailure(string kind, string code)
    {
        var element = JsonSerializer.SerializeToElement(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["kind"] = kind
        });
        var canonical = Canonicalize(element);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static bool IsAllowedBusinessError(string schemaSnapshot, string code)
    {
        try
        {
            using var schema = JsonDocument.Parse(schemaSnapshot);
            return schema.RootElement.TryGetProperty("businessErrors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), code, StringComparison.Ordinal));
        }
        catch (JsonException) { throw new ExternalTaskPayloadException("result_schema_invalid"); }
    }

    private static void ValidateOutputSchema(JsonElement result, string schemaSnapshot)
    {
        if (result.ValueKind != JsonValueKind.Object)
            throw new ExternalTaskPayloadException("result_schema_invalid");
        try
        {
            using var schema = JsonDocument.Parse(schemaSnapshot, new JsonDocumentOptions { MaxDepth = MaximumDepth });
            if (!schema.RootElement.TryGetProperty("output", out var output)
                || !output.TryGetProperty("properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
                throw new ExternalTaskPayloadException("result_schema_invalid");
            var allowed = properties.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
            var actual = result.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
            var required = output.TryGetProperty("required", out var requiredElement)
                ? requiredElement.EnumerateArray().Select(item => item.GetString()!).ToArray() : [];
            if (actual.Keys.Any(name => !allowed.ContainsKey(name)) || required.Any(name => !actual.ContainsKey(name)))
                throw new ExternalTaskPayloadException("result_schema_invalid");
            foreach (var pair in actual)
            {
                var field = allowed[pair.Key];
                var type = field.GetProperty("type").GetString();
                var valid = type switch
                {
                    "string" => pair.Value.ValueKind == JsonValueKind.String
                        && (!field.TryGetProperty("maxLength", out var max)
                            || pair.Value.GetString()!.Length <= max.GetInt32()),
                    "boolean" => pair.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                    "integer" => pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetInt64(out _),
                    _ => false
                };
                if (!valid) throw new ExternalTaskPayloadException("result_schema_invalid");
            }
        }
        catch (ExternalTaskPayloadException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ExternalTaskPayloadException("result_schema_invalid");
        }
    }

    private static void ValidateJson(JsonElement value, int depth)
    {
        if (depth > MaximumDepth) throw new ExternalTaskPayloadException("result_schema_invalid");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ExternalTaskPayloadException("duplicate_json_property");
                ValidateJson(property.Value, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) ValidateJson(item, depth + 1);
    }

    private static string Canonicalize(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(writer, value);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), skipInputValidation: false); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new ExternalTaskPayloadException("result_schema_invalid");
        }
    }
}

internal sealed class ExternalTaskPayloadException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
