using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentFormDefinitionService(BpmnDbContext db, IAuditLogService auditLogService) : IFormDefinitionService
{
    public async Task<IReadOnlyList<FormDefinitionMetadata>> ListAsync(string tenantId, CancellationToken ct = default) =>
        (await db.FormDefinitions.AsNoTracking().Where(x => x.TenantId == Tenant(tenantId)).OrderBy(x => x.Name).ToListAsync(ct)).Select(ToMetadata).ToList();
    public async Task<FormDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken ct = default) { var item = await db.FormDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == Tenant(tenantId) && (x.Id == id || x.Key == id), ct); return item is null ? null : ToMetadata(item); }
    public async Task<FormDefinitionMetadata> CreateAsync(string tenantId, FormDefinitionWriteRequest request, CancellationToken ct = default)
    { var tenant = Tenant(tenantId); var normalized = Normalize(request); if (await db.FormDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == normalized.Key, ct)) throw new FormDefinitionConflictException($"A form with key '{normalized.Key}' already exists."); var now = DateTime.UtcNow; var item = new FormDefinitionRecord { TenantId = tenant, Key = normalized.Key, Name = normalized.Name, Schema = normalized.Schema, CreatedAt = now, LastModified = now }; db.FormDefinitions.Add(item); await db.SaveChangesAsync(ct); await Audit("form.created", item, ct); return ToMetadata(item); }
    public async Task<bool> UpdateAsync(string tenantId, string id, FormDefinitionWriteRequest request, CancellationToken ct = default)
    { var tenant = Tenant(tenantId); var item = await db.FormDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct); if (item is null) return false; var normalized = Normalize(request); if (await db.FormDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == normalized.Key && x.Id != id, ct)) throw new FormDefinitionConflictException($"A form with key '{normalized.Key}' already exists."); item.Key = normalized.Key; item.Name = normalized.Name; item.Schema = normalized.Schema; item.Version++; item.LastModified = DateTime.UtcNow; await db.SaveChangesAsync(ct); await Audit("form.updated", item, ct); return true; }
    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken ct = default) { var item = await db.FormDefinitions.SingleOrDefaultAsync(x => x.TenantId == Tenant(tenantId) && x.Id == id, ct); if (item is null) return false; db.FormDefinitions.Remove(item); await db.SaveChangesAsync(ct); await Audit("form.deleted", item, ct); return true; }
    private static FormDefinitionWriteRequest Normalize(FormDefinitionWriteRequest request) { var key = Required(request.Key, "Key", 128); var name = Required(request.Name, "Name", 256); var schema = Required(request.Schema, "Schema", 200000); try { using var _ = JsonDocument.Parse(schema); } catch (JsonException e) { throw new ArgumentException("Schema must be valid JSON.", nameof(request), e); } return new(key, name, schema); }
    private static string Tenant(string? value) => Required(value, "TenantId", 64);
    private static string Required(string? value, string field, int length) { var text = value?.Trim(); if (string.IsNullOrWhiteSpace(text) || text.Length > length) throw new ArgumentException($"{field} is required and must not exceed {length} characters.", field); return text; }
    private static FormDefinitionMetadata ToMetadata(FormDefinitionRecord x) => new(x.Id, x.TenantId, x.Key, x.Name, x.Schema, x.Version, x.CreatedAt, x.LastModified);
    private Task Audit(string action, FormDefinitionRecord x, CancellationToken ct) => auditLogService.RecordAsync(new AuditLog { Timestamp = DateTimeOffset.UtcNow, Action = action, Resource = "form", ResourceId = x.Id, TenantId = x.TenantId, StatusCode = 200 }, ct);
}
