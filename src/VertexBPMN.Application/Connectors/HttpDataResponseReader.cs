using System.Text;
using System.Text.Json;

namespace VertexBPMN.Application.Connectors;

// LC03: bounded, content-type-aware JSON/text response reader. Reads at most
// maxResponseBytes, refuses oversized bodies with a defined error (no partial
// reading/mapping), decodes per Content-Type/encoding, parses JSON preserving
// scalar types and applies deep redaction so sensitive fields never reach
// process variables/history.
public sealed class HttpDataResponseReader(int maxResponseBytes, ConnectorRedactionPolicy redaction)
{
	public async Task<HttpDataResponse> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var contentType = response.Content.Headers.ContentType?.MediaType;
		var charsetName = response.Content.Headers.ContentType?.CharSet;
		var encoding = ResolveEncoding(charsetName);

		var (raw, tooLarge) = await ReadBoundedAsync(response, maxResponseBytes, encoding, cancellationToken);
		if (tooLarge)
		{
			return new HttpDataResponse(contentType, charsetName, string.Empty, IsJson: false, Success: false, "response_too_large", new Dictionary<string, object>(StringComparer.Ordinal));
		}

		if (!IsJsonContentType(contentType))
		{
			return new HttpDataResponse(contentType, charsetName, raw, IsJson: false, Success: true, null,
				new Dictionary<string, object>(StringComparer.Ordinal) { ["body"] = raw });
		}

		JsonElement element;
		try
		{
			using var document = JsonDocument.Parse(raw);
			element = document.RootElement.Clone();
		}
		catch (JsonException)
		{
			return new HttpDataResponse(contentType, charsetName, string.Empty, IsJson: true, Success: false, "invalid_body", new Dictionary<string, object>(StringComparer.Ordinal));
		}

		if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
		{
			return new HttpDataResponse(contentType, charsetName, string.Empty, IsJson: true, Success: false, "invalid_body", new Dictionary<string, object>(StringComparer.Ordinal));
		}

		var fields = ConvertNode(element);
		IReadOnlyDictionary<string, object> fieldMap =
			fields is Dictionary<string, object> map
				? redaction.Redact(map)
				: redaction.Redact(new Dictionary<string, object>(StringComparer.Ordinal) { ["body"] = fields });

		return new HttpDataResponse(contentType, charsetName, raw, IsJson: true, Success: true, null, fieldMap);
	}

	private static async Task<(string Body, bool TooLarge)> ReadBoundedAsync(
		HttpResponseMessage response, int maxResponseBytes, Encoding encoding, CancellationToken cancellationToken)
	{
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var buffer = new MemoryStream();
		var chunk = new byte[8192];
		long total = 0;
		while (true)
		{
			var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
			if (read == 0)
			{
				break;
			}

			total += read;
			if (total > maxResponseBytes)
			{
				return (string.Empty, true);
			}

			await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
		}

		return (encoding.GetString(buffer.ToArray()), false);
	}

	private static Encoding ResolveEncoding(string? charsetName)
	{
		if (string.IsNullOrWhiteSpace(charsetName))
		{
			return Encoding.UTF8;
		}

		try
		{
			return Encoding.GetEncoding(charsetName);
		}
		catch (ArgumentException)
		{
			return Encoding.UTF8; // unknown charset -> best-effort UTF-8
		}
	}

	private static bool IsJsonContentType(string? contentType) =>
		!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

	private static object ConvertNode(JsonElement element) =>
		element.ValueKind switch
		{
			JsonValueKind.Object => ToMap(element),
			JsonValueKind.Array => ToList(element),
			JsonValueKind.String => element.GetString() ?? string.Empty,
			JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null or JsonValueKind.Undefined => "<null>",
			_ => element.GetRawText()
		};

	private static Dictionary<string, object> ToMap(JsonElement obj)
	{
		var result = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var property in obj.EnumerateObject())
		{
			result[property.Name] = ConvertNode(property.Value);
		}

		return result;
	}

	private static System.Collections.Generic.List<object> ToList(JsonElement array)
	{
		var result = new System.Collections.Generic.List<object>();
		foreach (var item in array.EnumerateArray())
		{
			result.Add(ConvertNode(item));
		}

		return result;
	}
}
