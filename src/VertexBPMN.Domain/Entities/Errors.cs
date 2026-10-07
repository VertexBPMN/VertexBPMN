namespace VertexBPMN.Domain.Entities;

// Predefined errors (avoids magic strings)
public static class Errors
{
	public static Error AccountNotFound { get; } = new("AccountNotFound", ErrorType.NotFound, "Account not found.");
	public static Error InsufficientFunds { get; } = new("InsufficientFunds", ErrorType.Validation, "Insufficient balance.");
}
