using VertexBPMN.Application.Connectors;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class FieldMappingPreviewServiceTests
{

	[Fact]
	public void AppliesMappingsToParsedJsonSample()
	{
		const string json = """
			{
			  "customer": { "name": "A", "group": "gold" },
			  "amount": 12.50,
			  "rows": [ { "name": "first" } ]
			}
			""";

		var result = FieldMappingPreviewService.Apply(json,
		[
			new FieldMapping("customer.name", "customerName"),
			new FieldMapping("customer.group", "group"),
			new FieldMapping("amount", "amount", FieldTransform.ToNumber),
			new FieldMapping("rows.0.name", "firstName")
		]);

		Assert.True(result.Success);
		Assert.Equal("A", result.Values["customerName"]);
		Assert.Equal("gold", result.Values["group"]);
		Assert.Equal(12.50m, result.Values["amount"]);
		Assert.Equal("first", result.Values["firstName"]);
	}

	[Fact]
	public void InvalidJson_ReturnsResultWithoutMappingAndWithError()
	{
		var result = FieldMappingPreviewService.Apply("{ not json", [new FieldMapping("a", "b")]);

		Assert.False(result.Success);
		var error = Assert.Single(result.Errors);
		Assert.Contains("valid JSON", error, StringComparison.Ordinal);
	}

	[Fact]
	public void NullOrEmptyJson_YieldsNullSourceValues()
	{
		var result = FieldMappingPreviewService.Apply(null, [new FieldMapping("customer", "customer")]);

		Assert.True(result.Success);
		Assert.Null(result.Values["customer"]);
	}
}
