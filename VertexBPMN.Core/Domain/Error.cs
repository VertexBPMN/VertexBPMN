namespace VertexBPMN.Core.Domain;

public record Error(string Id, ErrorType Type, string Description);


public enum ErrorType { NotFound, Validation, Unauthorized }

// Predefined errors (avoids magic strings)
public static class Errors
{
    public static Error AccountNotFound { get; } = new("AccountNotFound", ErrorType.NotFound, "Account not found.");
    public static Error InsufficientFunds { get; } = new("InsufficientFunds", ErrorType.Validation, "Insufficient balance.");
}