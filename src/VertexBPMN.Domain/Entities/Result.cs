namespace VertexBPMN.Domain.Entities;

public record Result
{
	public bool IsSuccess { get; }
	public Error? Error { get; }

	protected Result(bool isSuccess, Error? error)
	{
		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, null);
	public static Result Failure(Error error) => new(false, error ?? throw new ArgumentNullException(nameof(error)));
	public static Result FromError(Error error) => Failure(error);

	public static implicit operator Result(Error error) => Failure(error);
}

public record Result<T> : Result
{
	public T? Value { get; }

	private Result(T value) : base(true, null) => Value = value;
	private Result(Error error) : base(false, error) { }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Named alternative to the existing generic conversion operator, required by CA2225; preserves the public Result<T> conversion contract.")]
	public static Result<T> FromT(T value) => new(value);
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Named alternative to the existing generic conversion operator, required by CA2225; preserves the public Result<T> conversion contract.")]
	public static new Result<T> FromError(Error error) => new(error);

	public static implicit operator Result<T>(T value) => new(value);

	public static implicit operator Result<T>(Error error) => new(error);
}
