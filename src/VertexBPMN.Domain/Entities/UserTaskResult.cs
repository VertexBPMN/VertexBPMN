namespace VertexBPMN.Domain.Entities;

public class UserTaskResult
{
	public bool Success { get; set; }
	public Guid TaskId { get; set; }
	public UserTaskStatus NewStatus { get; set; }
	public object? ResultData { get; set; }
	public string? ErrorMessage { get; set; }
}
