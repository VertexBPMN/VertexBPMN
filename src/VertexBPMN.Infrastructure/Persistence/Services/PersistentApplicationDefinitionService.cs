using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
namespace VertexBPMN.Infrastructure.Persistence.Services;
public sealed class PersistentApplicationDefinitionService(BpmnDbContext db, IAuditLogService auditLogService, int maxDraftsPerTenant = 100, ApplicationAccessContext? access = null) : IApplicationDefinitionService
{
    private Task<AuditLog> AuditAsync(string action, string tenantId, string id, CancellationToken cancellationToken) =>
        auditLogService.RecordAsync(new AuditLog { Action = action, TenantId = tenantId, ResourceId = id, Resource = "application", UserId = access?.ActorId, Timestamp = DateTimeOffset.UtcNow, StatusCode = 200 }, cancellationToken);

    private void Authorize(string permission)
    {
        if (access?.Permissions.Contains(permission) != true) { throw new UnauthorizedAccessException("Application permission denied."); }
    }

    public async Task<IReadOnlyList<ApplicationDefinitionSummary>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        // Lightweight listing: metadata only (no draft content) for this tenant, stable order.
        return await db.ApplicationDefinitions.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name).ThenBy(x => x.Key)
            .Select(x => new ApplicationDefinitionSummary(x.Id, x.TenantId, x.Key, x.Name, x.Version))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        var item = await db.ApplicationDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (item is null) { return false; }
        if (await db.ApplicationReleaseCandidates.AnyAsync(x => x.ApplicationDefinitionId == id, cancellationToken))
        { throw new ApplicationDefinitionConflictException("Release artifacts protect this application."); }
        var revisionIds = await db.ApplicationRevisions.Where(x => x.ApplicationDefinitionId == id && x.TenantId == tenantId).Select(x => x.Id).ToListAsync(cancellationToken);
        var otherSnapshots = await db.ApplicationRevisions.Where(x => x.ApplicationDefinitionId != id && x.TenantId == tenantId).Select(x => x.ContentJson).ToListAsync(cancellationToken);
        foreach (var snapshot in otherSnapshots)
        {
            using var document = JsonDocument.Parse(snapshot);
            if (document.RootElement.TryGetProperty("references", out var references)
                && references.EnumerateArray().Any(x => x.GetProperty("type").GetString() == "application" && revisionIds.Contains(x.GetProperty("id").GetString()!, StringComparer.Ordinal)))
            { throw new ApplicationDefinitionConflictException("Referenced application revisions cannot be deleted."); }
        }
        db.ApplicationDefinitions.Remove(item);
        db.ApplicationRevisions.RemoveRange(await db.ApplicationRevisions.Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == id).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("application.deleted", tenantId, id, cancellationToken);
        return true;
    }

    public async Task<ApplicationRevisionRecord?> GetReleasedRevisionAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var candidate = await db.ApplicationReleaseCandidates.AsNoTracking().Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == id && x.IsReleased)
            .OrderByDescending(x => x.ReleasedAtTicks).FirstOrDefaultAsync(cancellationToken);
        return candidate is null ? null : await db.ApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == candidate.ApplicationRevisionId && x.TenantId == tenantId, cancellationToken);
    }

    public async Task<ApplicationReleaseCandidateRecord> CreateReleaseCandidateAsync(string tenantId, string id, int version, CancellationToken cancellationToken = default)
    {
        Authorize("application.publish");
        var revision = await GetRevisionAsync(tenantId, id, version, cancellationToken)
            ?? throw new ApplicationDefinitionConflictException("Revision not found.");
        if (revision.DiagnosticsJson != "[]") { throw new ApplicationValidationException("Invalid domain draft."); }
        var candidate = new ApplicationReleaseCandidateRecord
        {
            TenantId = tenantId, ApplicationDefinitionId = id, ApplicationRevisionId = revision.Id,
            ManifestJson = await BuildManifestAsync(tenantId, id, revision, cancellationToken)
        };
        db.ApplicationReleaseCandidates.Add(candidate);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("application.prepared", tenantId, candidate.Id, cancellationToken);
        return candidate;
    }

    public Task<ApplicationReleaseCandidateRecord?> GetReleaseCandidateAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        return db.ApplicationReleaseCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
    }

    public async Task<bool> ReleaseAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        Authorize("application.publish");
        var candidate = await db.ApplicationReleaseCandidates.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (candidate is null) { return false; }
        var revision = await db.ApplicationRevisions.AsNoTracking().SingleAsync(x => x.Id == candidate.ApplicationRevisionId && x.TenantId == tenantId, cancellationToken);
        await ValidateMetadataAsync(tenantId, revision.ContentJson, cancellationToken);
        if (ApplicationDomainDiagnostics.Validate(revision.ContentJson) != "[]") { throw new ApplicationValidationException("Invalid revision."); }
        candidate.IsReleased = true;
        candidate.ReleasedAtTicks = DateTime.UtcNow.Ticks;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("application.released", tenantId, id, cancellationToken);
        return true;
    }

    public async Task<ApplicationDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        var item = await db.ApplicationDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (item is null) { return null; }
        var diagnostics = await db.ApplicationRevisions.AsNoTracking().Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == id && x.Version == item.Version)
            .Select(x => x.DiagnosticsJson).SingleAsync(cancellationToken);
        return new(item.Id, item.TenantId, item.Key, item.Name, item.Version, item.ContentJson, diagnostics);
    }

    public Task<ApplicationRevisionRecord?> GetRevisionAsync(string tenantId, string id, int version, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        return db.ApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == id && x.Version == version, cancellationToken);
    }

    public async Task<bool> UpdateAsync(string tenantId, string id, ApplicationDefinitionWriteRequest request, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        var item = await db.ApplicationDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (item is null) { return false; }
        if (item.Version != request.ExpectedVersion) { throw new ApplicationDefinitionConflictException("The application revision has changed."); }
        var content = await EnrichContentReferencesAsync(tenantId, request.Content, cancellationToken);
        await ValidateMetadataAsync(tenantId, content, cancellationToken);
        item.Version++;
        item.Key = request.Key;
        item.Name = request.Name;
        item.ContentJson = content;
        db.ApplicationRevisions.Add(new ApplicationRevisionRecord
        {
            ApplicationDefinitionId = id, TenantId = tenantId, Version = item.Version, ContentJson = content,
            DiagnosticsJson = ApplicationDomainDiagnostics.Validate(content),
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new ApplicationDefinitionConflictException("The application revision has changed."); }
        await AuditAsync("application.updated", tenantId, id, cancellationToken);
        return true;
    }

    private async Task ValidateMetadataAsync(string tenantId, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 64 || content.Length > 500_000)
        { throw new ApplicationValidationException("Tenant or metadata size is invalid."); }
        try { await ValidateMetadataCoreAsync(tenantId, content, cancellationToken); }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new ApplicationValidationException("Malformed application metadata."); }
    }

    private async Task ValidateMetadataCoreAsync(string tenantId, string content, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(content);
        if (document.RootElement.TryGetProperty("references", out var references))
        {
            foreach (var reference in references.EnumerateArray())
            {
                if (reference.GetProperty("tenantId").GetString() != tenantId)
                { throw new ApplicationForeignTenantException("Foreign tenant reference."); }
                var id = reference.GetProperty("id").GetString();
                var kind = reference.GetProperty("type").GetString();
                var owner = kind switch
                {
                    "process" when Guid.TryParse(id, out var processId) => await db.ProcessDefinitions.Where(x => x.Id == processId).Select(x => x.TenantId).SingleOrDefaultAsync(cancellationToken),
                    "form" => await db.FormDefinitionRevisions.Where(x => x.Id == id).Select(x => x.TenantId).SingleOrDefaultAsync(cancellationToken),
                    "application" => await db.ApplicationRevisions.Where(x => x.Id == id).Select(x => x.TenantId).SingleOrDefaultAsync(cancellationToken),
                    _ => null
                };
                if (owner is not null && owner != tenantId)
                { throw new ApplicationForeignTenantException("Reference belongs to another tenant."); }
                if (owner is null) { throw new ApplicationValidationException("Unknown reference revision."); }
            }
        }
        ApplicationComponentCatalog.Validate(content);
    }

    private async Task<string> BuildManifestAsync(string tenantId, string id, ApplicationRevisionRecord revision, CancellationToken cancellationToken)
    {
        await ValidateMetadataAsync(tenantId, revision.ContentJson, cancellationToken);
        using var document = JsonDocument.Parse(revision.ContentJson);
        var artifacts = new List<object>();
        var dependencies = new List<string>();
        if (document.RootElement.TryGetProperty("references", out var references))
        {
            foreach (var reference in references.EnumerateArray())
            {
                var referenceId = reference.GetProperty("id").GetString()!;
                var kind = reference.GetProperty("type").GetString();
                dependencies.Add(referenceId);
                if (kind == "form")
                {
                    var form = await db.FormDefinitionRevisions.AsNoTracking().SingleAsync(x => x.Id == referenceId && x.TenantId == tenantId, cancellationToken);
                    artifacts.Add(new { kind, key = form.Key, revisionId = form.Id, revision = form.Version, hash = form.SchemaHash });
                }
                else if (kind == "process")
                {
                    var processId = Guid.Parse(referenceId);
                    var process = await db.ProcessDefinitions.AsNoTracking().SingleAsync(x => x.Id == processId && x.TenantId == tenantId, cancellationToken);
                    artifacts.Add(new { kind, key = process.Key, revisionId = referenceId, revision = process.Version, hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(process.BpmnXml))).ToLowerInvariant() });
                }
                else
                {
                    var application = await db.ApplicationRevisions.AsNoTracking().SingleAsync(x => x.Id == referenceId && x.TenantId == tenantId, cancellationToken);
                    artifacts.Add(new { kind, revisionId = application.Id, revision = application.Version, hash = application.ContentHash });
                }
            }
        }
        artifacts.Insert(0, new { kind = "application", revisionId = revision.Id, revision = revision.Version, hash = revision.ContentHash, dependencies });
        return JsonSerializer.Serialize(new { schemaVersion = 1, tenantId, applicationId = id, artifacts });
    }

    public async Task<ApplicationDefinitionMetadata> CreateAsync(string tenantId, ApplicationDefinitionWriteRequest request, CancellationToken cancellationToken = default)
    {
        Authorize("application.design");
        if (request.ExpectedVersion != 0) { throw new ApplicationDefinitionConflictException("New definitions require expected version zero."); }
        if (await db.ApplicationDefinitions.CountAsync(x => x.TenantId == tenantId, cancellationToken) >= maxDraftsPerTenant)
        { throw new ApplicationDefinitionConflictException("Application draft quota exceeded."); }
        var content = await EnrichContentReferencesAsync(tenantId, request.Content, cancellationToken);
        await ValidateMetadataAsync(tenantId, content, cancellationToken);
        var definition = new ApplicationDefinitionRecord
        {
            TenantId = tenantId, Key = request.Key, Name = request.Name, ContentJson = content, Version = 1
        };
        db.ApplicationDefinitions.Add(definition);
        db.ApplicationRevisions.Add(new ApplicationRevisionRecord
        {
            ApplicationDefinitionId = definition.Id, TenantId = tenantId, Version = 1, ContentJson = content,
            DiagnosticsJson = ApplicationDomainDiagnostics.Validate(content),
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new ApplicationDefinitionConflictException("Application key already exists."); }
        await auditLogService.RecordAsync(new AuditLog { Action = "application.created", ResourceId = definition.Id, TenantId = tenantId }, cancellationToken);
        return new(definition.Id, tenantId, definition.Key, definition.Name, definition.Version, definition.ContentJson);
    }

    /// <summary>
    /// The LC07 designer serializes process references as { type, key } (see
    /// AppDesignerDocument.ToJson): it has no knowledge of the internal ProcessDefinition
    /// GUID, only the stable process key the runtime starts by. The server gate (and the
    /// manifest builder) require a process reference to carry the internal id + tenantId.
    /// This boundary translation resolves each unresolved process reference by key against
    /// the tenant's ProcessDefinitions, keeping the stable key untouched so the runtime can
    /// still start the process by it.
    /// </summary>
    private async Task<string> EnrichContentReferencesAsync(string tenantId, string content, CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(content);
        if (root?["references"] is not JsonArray references) { return content; }
        var needsEnrichment = references.OfType<JsonObject>().Any(reference =>
            reference["type"]?.GetValue<string>() == "process"
            && reference.ContainsKey("key")
            && (reference["id"] is null || string.IsNullOrEmpty(reference["id"]?.GetValue<string>())));
        if (!needsEnrichment) { return content; }

        foreach (var node in references)
        {
            if (node is not JsonObject reference || reference["type"]?.GetValue<string>() != "process") { continue; }
            var key = reference["key"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key)) { continue; }
            if (reference.ContainsKey("tenantId") && !string.IsNullOrEmpty(reference["tenantId"]?.GetValue<string>())) { continue; }
            var process = await db.ProcessDefinitions.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Key == key)
                .Select(x => new { x.Id, x.TenantId })
                .SingleOrDefaultAsync(cancellationToken);
            if (process is null) { continue; }
            reference["id"] = process.Id.ToString();
            reference["tenantId"] = process.TenantId;
        }
        return root.ToJsonString();
    }
}
