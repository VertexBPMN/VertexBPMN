namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Lightweight application-metadata summary returned by the applications list.
/// Deliberately omits the (potentially large) draft content so the listing stays
/// cheap; the full draft is fetched on demand via Get.
/// </summary>
public sealed record ApplicationDefinitionSummary(string Id, string TenantId, string Key, string Name, int Version);
