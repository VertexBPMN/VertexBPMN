using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentConnectorTemplateService(BpmnDbContext db, IAuditLogService auditLogService) : IConnectorTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ConnectorTemplateMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        return (await db.ConnectorTemplates.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync(cancellationToken)).Select(ToMetadata).ToList();
    }

    public async Task<ConnectorTemplateMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var record = await FindAsync(tenantId, id, cancellationToken);
        return record is null ? null : ToMetadata(record);
    }

    public async Task<ConnectorTemplateMetadata> CreateAsync(string tenantId, ConnectorTemplateWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var normalized = Normalize(request);
        if (await db.ConnectorTemplates.AnyAsync(x => x.TenantId == tenantId && x.Name == normalized.Name, cancellationToken))
            throw new ConnectorTemplateConflictException($"A connector template named '{normalized.Name}' already exists in this tenant.");
        var now = DateTime.UtcNow;
        var record = new ConnectorTemplateRecord { TenantId = tenantId, Name = normalized.Name, Category = normalized.Category, AppliesToJson = JsonSerializer.Serialize(normalized.AppliesTo, JsonOptions), Runtime = normalized.Runtime, Icon = normalized.Icon, PropertiesJson = JsonSerializer.Serialize(normalized.Properties, JsonOptions), CreatedAt = now, LastModified = now };
        db.ConnectorTemplates.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector_template.created", record, cancellationToken);
        return ToMetadata(record);
    }

    public async Task<bool> UpdateAsync(string tenantId, string id, ConnectorTemplateWriteRequest request, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        var normalized = Normalize(request);
        if (await db.ConnectorTemplates.AnyAsync(x => x.TenantId == tenantId && x.Name == normalized.Name && x.Id != id, cancellationToken))
            throw new ConnectorTemplateConflictException($"A connector template named '{normalized.Name}' already exists in this tenant.");
        record.Name = normalized.Name; record.Category = normalized.Category; record.Runtime = normalized.Runtime; record.Icon = normalized.Icon; record.AppliesToJson = JsonSerializer.Serialize(normalized.AppliesTo, JsonOptions); record.PropertiesJson = JsonSerializer.Serialize(normalized.Properties, JsonOptions); record.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector_template.updated", record, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        db.ConnectorTemplates.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector_template.deleted", record, cancellationToken);
        return true;
    }

    private Task<ConnectorTemplateRecord?> FindAsync(string tenantId, string id, CancellationToken cancellationToken) => db.ConnectorTemplates.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
    private async Task AuditAsync(string action, ConnectorTemplateRecord record, CancellationToken ct) => await auditLogService.RecordAsync(new AuditLog { Timestamp = DateTimeOffset.UtcNow, Action = action, Resource = "connector-template", ResourceId = record.Id, TenantId = record.TenantId, StatusCode = 200 }, ct);
    private static ConnectorTemplateMetadata ToMetadata(ConnectorTemplateRecord x) => new(x.Id, x.TenantId, x.Name, x.Category, JsonSerializer.Deserialize<List<string>>(x.AppliesToJson, JsonOptions) ?? [], x.Runtime, x.Icon, JsonSerializer.Deserialize<List<ConnectorTemplateProperty>>(x.PropertiesJson, JsonOptions) ?? [], x.CreatedAt, x.LastModified);
    private static ConnectorTemplateWriteRequest Normalize(ConnectorTemplateWriteRequest request)
    {
        var name = Required(request.Name, "Name", 256); var category = Required(request.Category, "Category", 128); var runtime = Required(request.Runtime, "Runtime", 128);
        var appliesTo = (request.AppliesTo ?? []).Select(x => Required(x, "AppliesTo", 128)).Distinct(StringComparer.Ordinal).ToList();
        if (appliesTo.Count == 0) throw new ArgumentException("At least one BPMN element type is required.", nameof(request));
        var properties = (request.Properties ?? []).Select(x => x with { Key = Required(x.Key, "Property key", 128), Type = Required(x.Type, "Property type", 64) }).ToList();
        if (properties.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != properties.Count) throw new ArgumentException("Template property keys must be unique.", nameof(request));
        return new(name, category, appliesTo, runtime, string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(), properties);
    }
    private static string Required(string? value, string field, int maxLength) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength) throw new ArgumentException($"{field} is required and must not exceed {maxLength} characters.", field); return normalized; }
    private static void ValidateTenant(string tenantId) => _ = Required(tenantId, "TenantId", 64);
}
