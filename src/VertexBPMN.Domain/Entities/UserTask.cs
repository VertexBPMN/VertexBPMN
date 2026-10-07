namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a user or service task instance.
/// </summary>
public class UserTask
{
	public Guid Id { get; set; }
	public Guid ProcessInstanceId { get; set; }
	public string ActivityId { get; set; } = string.Empty;
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
	public long Revision { get; set; }
	public Guid? MultiInstanceExecutionId { get; set; }
	public int? MultiInstanceIndex { get; set; }
	public Dictionary<string, object> LocalVariables { get; set; } = new(StringComparer.Ordinal);
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
