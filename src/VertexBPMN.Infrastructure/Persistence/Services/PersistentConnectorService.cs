using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentConnectorService(
    BpmnDbContext db,
    ICredentialService credentialService,
    IAuditLogService auditLogService) : IConnectorService
{
    public async Task<IReadOnlyList<ConnectorMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var records = await db.Connectors.AsNoTracking()
            .Where(connector => connector.TenantId == tenantId)
            .OrderBy(connector => connector.Name)
            .ToListAsync(cancellationToken);
        return records.Select(ToMetadata).ToList();
    }

    public async Task<ConnectorMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var record = await FindAsync(tenantId, id, cancellationToken);
        return record is null ? null : ToMetadata(record);
    }

    public async Task<ConnectorMetadata> CreateAsync(string tenantId, ConnectorWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var normalized = await NormalizeAsync(tenantId, request, cancellationToken);
        if (await db.Connectors.AnyAsync(c => c.TenantId == tenantId && c.Name == normalized.Name, cancellationToken))
            throw new ConnectorConflictException($"A connector named '{normalized.Name}' already exists in this tenant.");

        var now = DateTime.UtcNow;
        var record = new ConnectorRecord
        {
            TenantId = tenantId,
            Name = normalized.Name,
            Type = normalized.Type,
            Description = normalized.Description,
            Endpoint = normalized.Endpoint,
            CredentialId = normalized.CredentialId,
            Enabled = normalized.Enabled,
            CreatedAt = now,
            LastModified = now
        };
        db.Connectors.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector.created", record, cancellationToken);
        return ToMetadata(record);
    }

    public async Task<bool> UpdateAsync(string tenantId, string id, ConnectorWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        var normalized = await NormalizeAsync(tenantId, request, cancellationToken);
        if (await db.Connectors.AnyAsync(c => c.TenantId == tenantId && c.Name == normalized.Name && c.Id != id, cancellationToken))
            throw new ConnectorConflictException($"A connector named '{normalized.Name}' already exists in this tenant.");

        record.Name = normalized.Name;
        record.Type = normalized.Type;
        record.Description = normalized.Description;
        record.Endpoint = normalized.Endpoint;
        record.CredentialId = normalized.CredentialId;
        record.Enabled = normalized.Enabled;
        record.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector.updated", record, cancellationToken, new { enabled = record.Enabled });
        return true;
    }

    public async Task<bool> SetEnabledAsync(string tenantId, string id, bool enabled, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        record.Enabled = enabled;
        record.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(enabled ? "connector.enabled" : "connector.disabled", record, cancellationToken, new { enabled });
        return true;
    }

    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        db.Connectors.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("connector.deleted", record, cancellationToken);
        return true;
    }

    private async Task<ConnectorWriteRequest> NormalizeAsync(string tenantId, ConnectorWriteRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "Name", 256);
        var type = Required(request.Type, "Type", 128);
        var description = Optional(request.Description, "Description", 2000);
        var endpoint = Optional(request.Endpoint, "Endpoint", 2048);
        if (endpoint is not null && (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new ArgumentException("Endpoint must be an absolute HTTP or HTTPS URL.", nameof(request));

        var credentialId = Optional(request.CredentialId, "CredentialId", 128);
        if (credentialId is not null && await credentialService.GetAsync(tenantId, credentialId, cancellationToken) is null)
            throw new ConnectorCredentialException("The credential reference does not exist in this tenant.");

        return new ConnectorWriteRequest(name, type, description, endpoint, credentialId, request.Enabled);
    }

    private Task<ConnectorRecord?> FindAsync(string tenantId, string id, CancellationToken cancellationToken) =>
        db.Connectors.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken);

    private async Task AuditAsync(string action, ConnectorRecord record, CancellationToken cancellationToken, object? details = null) =>
        await auditLogService.RecordAsync(new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            Resource = "connector",
            ResourceId = record.Id,
            TenantId = record.TenantId,
            StatusCode = 200,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details)
        }, cancellationToken);

    private static ConnectorMetadata ToMetadata(ConnectorRecord record) =>
        new(record.Id, record.TenantId, record.Name, record.Type, record.Description, record.Endpoint,
            record.CredentialId, record.Enabled, record.CreatedAt, record.LastModified);

    private static string Required(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException($"{field} is required.", field);
        if (normalized.Length > maxLength) throw new ArgumentException($"{field} exceeds the maximum length of {maxLength}.", field);
        return normalized;
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentException($"{field} exceeds the maximum length of {maxLength}.", field);
        return normalized;
    }

    private static void ValidateTenant(string tenantId) => _ = Required(tenantId, "TenantId", 64);
}

public sealed class ConnectorConflictException(string message) : Exception(message);
public sealed class ConnectorCredentialException(string message) : Exception(message);
