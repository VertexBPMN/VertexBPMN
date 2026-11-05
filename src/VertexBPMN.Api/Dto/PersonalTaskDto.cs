namespace VertexBPMN.Api.Dto;

/// <summary>
/// Minimized personal-inbox task DTO. It deliberately exposes no process variables,
/// no internal revision data and a status limited to the controlled projection
/// surface (see PersonalTaskStatus). It is never a raw <c>UserTask</c>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures and controller activation (PersonalTaskController).")]
public sealed class PersonalTaskDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public Guid ProcessInstanceId { get; set; }
	public string? TenantId { get; set; }
	public DateTime? Created { get; set; }
	public DateTime? Due { get; set; }
	public DateTime? Completed { get; set; }
	public string? FormKey { get; set; }
}
