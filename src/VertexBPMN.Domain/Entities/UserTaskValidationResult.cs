namespace VertexBPMN.Domain.Entities;


public sealed class UserTaskValidationResult
{
	public bool IsValid { get; }
	public IReadOnlyList<string> Errors { get; }

	private UserTaskValidationResult(bool isValid, IReadOnlyList<string> errors)
	{
		IsValid = isValid;
		Errors = errors;
	}

	public static UserTaskValidationResult Success() => new(true, Array.Empty<string>());

	public static UserTaskValidationResult Failure(IEnumerable<string> errors)
	{
		var list = errors.Where(e => !string.IsNullOrWhiteSpace(e))
			.Select(e => e.Trim())
			.Distinct(StringComparer.Ordinal)
			.ToList()
			.AsReadOnly();
		return new UserTaskValidationResult(false, list);
	}
}
