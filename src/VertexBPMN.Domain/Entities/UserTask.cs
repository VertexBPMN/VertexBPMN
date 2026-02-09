namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a user or service task instance.
/// </summary>
public class UserTask
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = string.Empty;
    public UserTaskStatus Status { get; set; } = UserTaskStatus.Pending;
    public List<string> CandidateUsers { get; set; } = new();
    public string CandidateRole { get; set; } = string.Empty;
    public List<string> RequiredFields { get; set; } = new();
    /// <summary>
    /// Camunda formKey for user task forms (form-js, embedded forms, etc.)
    /// </summary>
    public string? FormKey { get; set; }

    /// <summary>
    /// Optional JSON schema for dynamic forms (form-js, Camunda 8, etc.)
    /// </summary>
    public string? FormSchema { get; set; }

    // TODO: Add candidate users/groups, etc.
}


public class UserTaskContext
{
    public Guid TaskId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public UserTaskAction Action { get; set; }
    public Dictionary<string, object> TaskData { get; set; } = new();
    public string DelegatedUserId { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}

public class UserTaskResult
{
    public bool Success { get; set; }
    public Guid TaskId { get; set; }
    public UserTaskStatus NewStatus { get; set; }
    public object? ResultData { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum UserTaskStatus
{
    Pending,
    Completed,
    Delegated,
    Rejected,
    Cancelled
}

public enum UserTaskAction
{
    Complete,
    Delegate,
    Reject
}


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
            .Distinct()
            .ToList()
            .AsReadOnly();
        return new UserTaskValidationResult(false, list);
    }
}