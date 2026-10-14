using System.Net;
using System.Text;
using VertexBPMN.Application.Connectors;

namespace VertexBPMN.Tests.Unit.Application.Connectors;

public class HttpDataResponseReaderTests
{
	private static HttpDataResponseReader CreateReader(int maxBytes) => new(maxBytes, new ConnectorRedactionPolicy());

	private static HttpResponseMessage JsonResponse(string json)
	{
		var content = new StringContent(json, Encoding.UTF8, "application/json");
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
	}

	[Fact]
	public async Task ReadsBoundedJsonAndRedactsSensitiveNestedFields()
	{
		var reader = CreateReader(maxBytes: 4096);
		using var response = JsonResponse("""{"customer":"A","amount":12.5,"group":"basic","secretToken":"s3cr3t","nested":{"apiKey":"k"},"list":[{"token":"t1","v":1}]}""");

		var result = await reader.ReadAsync(response, TestContext.Current.CancellationToken);

		Assert.True(result.Success);
		Assert.Null(result.ErrorCode);
		Assert.Equal("A", result.Fields["customer"]);
		Assert.Equal(12.5, result.Fields["amount"]);
		Assert.Equal("basic", result.Fields["group"]);
		Assert.Equal("***", result.Fields["secretToken"]);
		Assert.Equal("***", ((Dictionary<string, object>)result.Fields["nested"])["apiKey"]);
		var list = (System.Collections.Generic.List<object>)result.Fields["list"];
		Assert.Equal("***", ((Dictionary<string, object>)list[0])["token"]);
		Assert.Equal(1L, ((Dictionary<string, object>)list[0])["v"]);
	}

	[Fact]
	public async Task RejectsOversizedResponseWithDefinedErrorAndNoPartialReading()
	{
		var reader = CreateReader(maxBytes: 16);
		var payload = new string('a', 100);
		using var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(payload, Encoding.UTF8, "text/plain")
		};

		var result = await reader.ReadAsync(response, TestContext.Current.CancellationToken);

		Assert.False(result.Success);
		Assert.Equal("response_too_large", result.ErrorCode);
		Assert.Empty(result.Fields);
	}

	[Fact]
	public async Task RejectsInvalidJsonWithDefinedErrorWithoutPartialMapping()
	{
		var reader = CreateReader(maxBytes: 4096);
		using var response = JsonResponse("""{"customer":"A","amount":12.5,"group" }}NOT-JSON""");

		var result = await reader.ReadAsync(response, TestContext.Current.CancellationToken);

		Assert.False(result.Success);
		Assert.Equal("invalid_body", result.ErrorCode);
		Assert.Empty(result.Fields);
	}

	[Fact]
	public async Task ReadsTextResponseAsBoundedString()
	{
		var reader = CreateReader(maxBytes: 256);
		using var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("hello world", Encoding.UTF8, "text/plain")
		};

		var result = await reader.ReadAsync(response, TestContext.Current.CancellationToken);

		Assert.True(result.Success);
		Assert.Equal("hello world", result.Fields["body"]);
	}
}
