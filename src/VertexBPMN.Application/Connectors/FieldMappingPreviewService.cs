using System.Text.Json;

namespace VertexBPMN.Application.Connectors;

/// <summary>
/// Computes a Studio mapper preview (LC03 Z.247): parses sample source JSON, then applies the
/// explicit field mappings via <see cref="FieldMapper"/>. Keeps parsing + mapping logic in the
/// Application layer so it is unit-testable; the API controller stays thin.
/// </summary>
public static class FieldMappingPreviewService
{
	public static FieldMappingResult Apply(string? sourceJson, IReadOnlyList<FieldMapping> mappings)
	{
		object? source = null;
		if (!string.IsNullOrWhiteSpace(sourceJson))
		{
			try
			{
				using var document = JsonDocument.Parse(sourceJson);
				source = ConvertNode(document.RootElement);
			}
			catch (JsonException)
			{
				return new FieldMappingResult(false, new Dictionary<string, object?>(StringComparer.Ordinal),
					["Source is not valid JSON."]);
			}
		}

		return FieldMapper.Apply(source, mappings);
	}

	private static object? ConvertNode(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				var map = new Dictionary<string, object?>(StringComparer.Ordinal);
				foreach (var property in element.EnumerateObject())
				{
					map[property.Name] = ConvertNode(property.Value);
				}
				return map;
			case JsonValueKind.Array:
				var list = new List<object?>();
				foreach (var item in element.EnumerateArray())
				{
					list.Add(ConvertNode(item));
				}
				return list;
			case JsonValueKind.String:
				return element.GetString();
			case JsonValueKind.Number:
				if (element.TryGetInt64(out var integer))
				{
					return integer;
				}
				return element.TryGetDecimal(out var dec) ? dec : element.GetDouble();
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			default:
				return element.GetRawText();
		}
	}
}
