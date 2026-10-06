namespace VertexBPMN.Domain.Entities;


public class UserTaskContext
{
	public Guid TaskId { get; set; }
	public Guid ProcessInstanceId { get; set; }
	public string UserId { get; set; } = string.Empty;
	public UserTaskAction Action { get; set; }
	public Dictionary<string, object> TaskData { get; set; } = new(StringComparer.Ordinal);
	public string DelegatedUserId { get; set; } = string.Empty;
	public string RejectionReason { get; set; } = string.Empty;
}
