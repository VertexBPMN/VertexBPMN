using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentFormDefinitionService(BpmnDbContext db, IAuditLogService auditLogService) : IFormDefinitionService
{
    public async Task<IReadOnlyList<FormDefinitionRevisionMetadata>> ListRevisionsAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var form = await GetAsync(tenantId, id, cancellationToken);
        if (form is null) { return []; }
        return (await db.FormDefinitionRevisions.AsNoTracking().Where(x => x.TenantId == Tenant(tenantId) && x.FormDefinitionId == form.Id)
            .OrderByDescending(x => x.Version).ToListAsync(cancellationToken)).Select(ToRevisionMetadata).ToList();
    }
    public async Task<FormDefinitionRevisionMetadata?> GetRevisionAsync(string tenantId, string id, int version, CancellationToken cancellationToken = default)
    {
        var form = await GetAsync(tenantId, id, cancellationToken);
        if (form is null) { return null; }
        var revision = await db.FormDefinitionRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == Tenant(tenantId) && x.FormDefinitionId == form.Id && x.Version == version, cancellationToken);
        return revision is null ? null : ToRevisionMetadata(revision);
    }
    private static FormDefinitionRevisionMetadata ToRevisionMetadata(FormDefinitionRevisionRecord x) =>
        new(x.Id, x.FormDefinitionId, x.TenantId, x.Key, x.Name, x.Schema, x.SchemaHash, x.Version, x.CreatedAt);
    public async Task<IReadOnlyList<FormDefinitionMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default) =>
        (await db.FormDefinitions.AsNoTracking().Where(x => x.TenantId == Tenant(tenantId)).OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(ToMetadata).ToList();
    public async Task<FormDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default) { var item = await db.FormDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == Tenant(tenantId) && (x.Id == id || x.Key == id), cancellationToken); return item is null ? null : ToMetadata(item); }
    public async Task<FormDefinitionMetadata> CreateAsync(string tenantId, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default)
    { var tenant = Tenant(tenantId); var normalized = Normalize(request); if (await db.FormDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == normalized.Key, cancellationToken)) { throw new FormDefinitionConflictException($"A form with key '{normalized.Key}' already exists."); } var now = DateTime.UtcNow; var item = new FormDefinitionRecord { TenantId = tenant, Key = normalized.Key, Name = normalized.Name, Schema = normalized.Schema, CreatedAt = now, LastModified = now }; db.FormDefinitions.Add(item); db.FormDefinitionRevisions.Add(Revision(item)); await SaveAsync(cancellationToken); await Audit("form.created", item, cancellationToken); return ToMetadata(item); }
    public async Task<bool> UpdateAsync(string tenantId, string id, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = Tenant(tenantId);
        var item = await db.FormDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenant && (x.Id == id || x.Key == id), cancellationToken);
        if (item is null) { return false; }
        var normalized = Normalize(request);
        if (await db.FormDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == normalized.Key && x.Id != item.Id, cancellationToken))
        { throw new FormDefinitionConflictException($"A form with key '{normalized.Key}' already exists."); }
        item.Key = normalized.Key; item.Name = normalized.Name; item.Schema = normalized.Schema;
        item.Version++; item.LastModified = DateTime.UtcNow;
        db.FormDefinitionRevisions.Add(Revision(item));
        await SaveAsync(cancellationToken);
        await Audit("form.updated", item, cancellationToken);
        return true;
    }
    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var tenant = Tenant(tenantId);
        var item = await db.FormDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenant && (x.Id == id || x.Key == id), cancellationToken);
        if (item is null) { return false; }
        var revisions = await db.FormDefinitionRevisions.Where(x => x.FormDefinitionId == item.Id).ToListAsync(cancellationToken);
        var revisionIds = revisions.Select(x => x.Id).ToArray();
        var keys = revisions.Select(x => x.Key).Append(item.Key).Append(item.Id).Distinct(StringComparer.Ordinal).ToArray();
        if (await db.Tasks.AnyAsync(x => x.TenantId == tenant && ((x.FormRevisionId != null && revisionIds.Contains(x.FormRevisionId)) || (x.FormKey != null && keys.Contains(x.FormKey))), cancellationToken))
        { throw new FormDefinitionConflictException("A referenced form cannot be deleted."); }
        db.FormDefinitions.Remove(item);
        await SaveAsync(cancellationToken);
        await Audit("form.deleted", item, cancellationToken);
        return true;
    }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new FormDefinitionConflictException("The form has changed. Reload before updating."); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new FormDefinitionConflictException("The form write conflicts with an existing revision or reference."); }
    }
    private static FormDefinitionWriteRequest Normalize(FormDefinitionWriteRequest request) { var key = Required(request.Key, "Key", 128); var name = Required(request.Name, "Name", 256); var schema = Required(request.Schema, "Schema", 200000); try { using var _ = JsonDocument.Parse(schema); } catch (JsonException e) { throw new ArgumentException("Schema must be valid JSON.", nameof(request), e); } return new(key, name, schema); }
    private static string Tenant(string? value) => Required(value, "TenantId", 64);
    private static string Required(string? value, string field, int length) { var text = value?.Trim(); if (string.IsNullOrWhiteSpace(text) || text.Length > length) { throw new ArgumentException($"{field} is required and must not exceed {length} characters.", field); } return text; }
    private static FormDefinitionRevisionRecord Revision(FormDefinitionRecord item) => new()
    {
        FormDefinitionId = item.Id, TenantId = item.TenantId, Key = item.Key, Name = item.Name,
        Schema = item.Schema, Version = item.Version, CreatedAt = item.LastModified,
        SchemaHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.Schema))).ToLowerInvariant()
    };
    private static FormDefinitionMetadata ToMetadata(FormDefinitionRecord x) => new(x.Id, x.TenantId, x.Key, x.Name, x.Schema, x.Version, x.CreatedAt, x.LastModified);
    private Task<AuditLog> Audit(string action, FormDefinitionRecord x, CancellationToken cancellationToken) => auditLogService.RecordAsync(new AuditLog { Timestamp = DateTimeOffset.UtcNow, Action = action, Resource = "form", ResourceId = x.Id, TenantId = x.TenantId, StatusCode = 200 }, cancellationToken);
}
