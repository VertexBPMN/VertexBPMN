using VertexBPMN.Application.Connectors;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class FieldMapperTests
{
	[Fact]
	public void CopiesScalarFields_ResolvesNestedAndListPaths()
	{
		var source = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["httpBody"] = new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["customer"] = "A",
				["amount"] = 12.5m,
				["nested"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["group"] = "gold" }
			},
			["rows"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "first" } }
		};

		var result = FieldMapper.Apply(source,
		[
			new FieldMapping("httpBody.customer", "customer"),
			new FieldMapping("httpBody.nested.group", "group"),
			new FieldMapping("rows.0.name", "firstName")
		]);

		Assert.True(result.Success);
		Assert.Equal("A", result.Values["customer"]);
		Assert.Equal("gold", result.Values["group"]);
		Assert.Equal("first", result.Values["firstName"]);
	}

	[Fact]
	public void MissingOrNullSource_SetsNull_WhenNotRequired()
	{
		var result = FieldMapper.Apply(
			new Dictionary<string, object?>(StringComparer.Ordinal) { ["present"] = null },
		[
			new FieldMapping("present", "a"),
			new FieldMapping("absent", "b")
		]);

		Assert.True(result.Success);
		Assert.Null(result.Values["a"]);
		Assert.Null(result.Values["b"]);
	}

	[Fact]
	public void RequiredMissingSource_FailsWholeMapping_NoPartialAssignment()
	{
		var result = FieldMapper.Apply(
			new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = "value" },
		[
			new FieldMapping("ok", "a"),
			new FieldMapping("absent", "b", FieldTransform.Copy, Required: true)
		]);

		Assert.False(result.Success);
		Assert.Empty(result.Values);
		var error = Assert.Single(result.Errors);
		Assert.Contains("'b'", error, StringComparison.Ordinal);
	}

	[Fact]
	public void ToNumber_ParsesDecimal_AndRejectsInvalid()
	{
		var ok = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = "12.50" },
			[new FieldMapping("v", "n", FieldTransform.ToNumber)]);
		Assert.True(ok.Success);
		Assert.Equal(12.50m, ok.Values["n"]);

		var bad = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = "abc" },
			[new FieldMapping("v", "n", FieldTransform.ToNumber)]);
		Assert.False(bad.Success);
	}

	[Fact]
	public void ToDate_ParsesIsoString_AndRejectsInvalid()
	{
		var ok = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = "2026-03-02T13:00:00Z" },
			[new FieldMapping("v", "d", FieldTransform.ToDate)]);
		Assert.True(ok.Success);
		Assert.Equal(new DateTimeOffset(2026, 3, 2, 13, 0, 0, TimeSpan.Zero), ok.Values["d"]);

		var bad = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = "not-a-date" },
			[new FieldMapping("v", "d", FieldTransform.ToDate)]);
		Assert.False(bad.Success);
	}

	[Fact]
	public void ToBool_ParsesVariants()
	{
		var ok = FieldMapper.Apply(
			new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = "true", ["b"] = "0", ["c"] = true },
			[
				new FieldMapping("a", "x", FieldTransform.ToBool),
				new FieldMapping("b", "y", FieldTransform.ToBool),
				new FieldMapping("c", "z", FieldTransform.ToBool)
			]);
		Assert.True(ok.Success);
		Assert.True((bool)ok.Values["x"]!);
		Assert.False((bool)ok.Values["y"]!);
		Assert.True((bool)ok.Values["z"]!);
	}

	[Fact]
	public void ToText_ConvertsScalar_RejectsListOrObject()
	{
		var ok = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = 12.5m },
			[new FieldMapping("v", "t", FieldTransform.ToText)]);
		Assert.True(ok.Success);
		Assert.Equal("12.5", ok.Values["t"]);

		var bad = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = new List<object?>() },
			[new FieldMapping("v", "t", FieldTransform.ToText)]);
		Assert.False(bad.Success);
	}

	[Fact]
	public void Copy_PassesThroughListAndObject()
	{
		var list = new List<object?> { "x" };
		var result = FieldMapper.Apply(new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = list },
			[new FieldMapping("v", "out")]);

		Assert.True(result.Success);
		Assert.Same(list, result.Values["out"]);
	}
}
