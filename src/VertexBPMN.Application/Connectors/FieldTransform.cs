namespace VertexBPMN.Application.Connectors;

/// <summary>Explicit field transformation applied by the Studio mapper (LC03 Z.247).</summary>
public enum FieldTransform
{
	/// <summary>Pass the resolved source value through unchanged (incl. lists/objects).</summary>
	Copy,
	/// <summary>Convert a scalar to its invariant string form; null stays null.</summary>
	ToText,
	/// <summary>Convert a scalar (number or numeric string) to <c>decimal?</c>.</summary>
	ToNumber,
	/// <summary>Convert a scalar (string or <see cref="DateTime"/>) to <c>DateTimeOffset?</c>.</summary>
	ToDate,
	/// <summary>Convert a scalar (bool or "true"/"false"/"1"/"0") to <c>bool?</c>.</summary>
	ToBool
}
