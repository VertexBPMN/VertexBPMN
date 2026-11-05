using System.Collections;
using System.Globalization;

namespace VertexBPMN.Application.Connectors;

/// <summary>
/// Applies explicit field mappings (LC03 Z.247) to a source value (a flat or nested
/// dictionary/list structure, e.g. an HTTP response body or a query row set). The mapper is
/// fail-closed: any required-missing source or failed explicit transformation aborts the whole
/// mapping, so no partially assigned outputs leak downstream.
/// </summary>
public static class FieldMapper
{
	public static FieldMappingResult Apply(object? source, IReadOnlyList<FieldMapping> mappings)
	{
		var outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
		var errors = new List<string>();

		foreach (var mapping in mappings)
		{
			Resolve(source, mapping.SourcePath, out var found, out var value);

			if (!found || value is null)
			{
				if (mapping.Required)
				{
					errors.Add($"'{mapping.TargetKey}': source '{mapping.SourcePath}' is missing or null.");
				}
				else
				{
					outputs[mapping.TargetKey] = null;
				}
				continue;
			}

			if (TryTransform(value, mapping.Transform, out var transformed, out var error))
			{
				outputs[mapping.TargetKey] = transformed;
			}
			else
			{
				errors.Add($"'{mapping.TargetKey}': {error}");
			}
		}

		return errors.Count == 0
			? new FieldMappingResult(true, outputs, [])
			: new FieldMappingResult(false, new Dictionary<string, object?>(StringComparer.Ordinal), errors);
	}

	private static void Resolve(object? source, string path, out bool found, out object? value)
	{
		var current = source;
		foreach (var segment in path.Split('.'))
		{
			if (current is null)
			{
				found = false;
				value = null;
				return;
			}

			var (nextFound, next) = Step(current, segment);
			if (!nextFound)
			{
				found = false;
				value = null;
				return;
			}

			current = next;
		}

		found = current is not null;
		value = current;
	}

	private static (bool Found, object? Value) Step(object current, string segment)
	{
		return current switch
		{
			IDictionary<string, object?> dict => dict.TryGetValue(segment, out var dv) ? (true, dv) : (false, null),
			IList list when int.TryParse(segment, out var index) && index >= 0 && index < list.Count => (true, list[index]),
			_ => (false, null)
		};
	}

	private static bool TryTransform(object value, FieldTransform transform, out object? result, out string? error)
	{
		switch (transform)
		{
			case FieldTransform.Copy:
				result = value;
				error = null;
				return true;
			case FieldTransform.ToText:
				if (IsScalar(value))
				{
					result = Convert.ToString(value, CultureInfo.InvariantCulture);
					error = null;
					return true;
				}
				error = "list/object cannot be converted to text; use Copy.";
				result = null;
				return false;
			case FieldTransform.ToNumber:
				if (ToNumber(value) is { } number)
				{
					result = number;
					error = null;
					return true;
				}
				error = $"'{value}' is not a valid decimal.";
				result = null;
				return false;
			case FieldTransform.ToDate:
				if (ToDate(value) is { } date)
				{
					result = date;
					error = null;
					return true;
				}
				error = $"'{value}' is not a valid date.";
				result = null;
				return false;
			case FieldTransform.ToBool:
				if (ToBool(value) is { } boolean)
				{
					result = boolean;
					error = null;
					return true;
				}
				error = $"'{value}' is not a valid boolean.";
				result = null;
				return false;
			default:
				error = $"unsupported transform '{transform}'.";
				result = null;
				return false;
		}
	}

	private static bool IsScalar(object value)
		=> value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset;

	private static decimal? ToNumber(object value) => value switch
	{
		decimal d => d,
		double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl) => (decimal)dbl,
		float f when !float.IsNaN(f) && !float.IsInfinity(f) => (decimal)f,
		byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
		string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
		_ => null
	};

	private static DateTimeOffset? ToDate(object value) => value switch
	{
		DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
		DateTimeOffset dto => dto,
		string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
		_ => null
	};

	private static bool? ToBool(object value) => value switch
	{
		bool b => b,
		string text when bool.TryParse(text, out var parsed) => parsed,
		"1" => true,
		"0" => false,
		_ => null
	};
}
