using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Minimized projection of a user task for the personal inbox. It deliberately
/// carries no <see cref="UserTask.LocalVariables"/> and no internal fields, so the
/// process state never leaks through the inbox surface.
/// </summary>
public sealed record TaskInboxRow(
	Guid Id,
	string Name,
	UserTaskStatus Status,
	Guid ProcessInstanceId,
	string? TenantId,
	DateTime CreatedAt,
	DateTime? DueDate,
	DateTime? CompletedAt,
	string? FormKey);
