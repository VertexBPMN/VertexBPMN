using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class PersistentCredentialService(
    BpmnDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IAuditLogService auditLogService,
    ILogger<PersistentCredentialService> logger) : ICredentialService
{
    private const int MaxSecretCount = 32;
    private const int MaxSecretKeyLength = 128;
    private const int MaxSecretValueLength = 16_384;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("VertexBPMN.Credentials.v1");

    public async Task<IReadOnlyList<CredentialMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var records = await db.Credentials.AsNoTracking().Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return records.Select(ToMetadata).ToList();
    }

    public async Task<CredentialMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        return record is null ? null : ToMetadata(record);
    }

    public async Task<CredentialMetadata> CreateAsync(string tenantId, CredentialWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var name = NormalizeRequired(request.Name, "Name", 256);
        var type = NormalizeRequired(request.Type, "Type", 128);
        var secrets = NormalizeSecrets(request.Secrets);
        if (await db.Credentials.AnyAsync(c => c.TenantId == tenantId && c.Name == name, cancellationToken))
            throw new CredentialConflictException($"A credential named '{name}' already exists in this tenant.");

        var now = DateTime.UtcNow;
        var record = new CredentialRecord
        {
            TenantId = tenantId,
            Name = name,
            Type = type,
            Description = NormalizeOptional(request.Description, 2000),
            SecretKeysJson = JsonSerializer.Serialize(secrets.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray()),
            ProtectedValues = Protect(secrets),
            CreatedAt = now,
            LastModified = now
        };
        db.Credentials.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("credential.created", record, cancellationToken);
        return ToMetadata(record);
    }

    public async Task<bool> UpdateMetadataAsync(string tenantId, string id, CredentialMetadataUpdate request, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        var name = NormalizeRequired(request.Name, "Name", 256);
        var type = NormalizeRequired(request.Type, "Type", 128);
        if (await db.Credentials.AnyAsync(c => c.TenantId == tenantId && c.Name == name && c.Id != id, cancellationToken))
            throw new CredentialConflictException($"A credential named '{name}' already exists in this tenant.");
        record.Name = name;
        record.Type = type;
        record.Description = NormalizeOptional(request.Description, 2000);
        record.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("credential.metadata_updated", record, cancellationToken);
        return true;
    }

    public async Task<bool> RotateSecretAsync(string tenantId, string id, CredentialSecretRotation request, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        var key = NormalizeRequired(request.Key, "Key", MaxSecretKeyLength);
        var value = NormalizeRequired(request.Value, "Value", MaxSecretValueLength, trim: false);
        var secrets = Unprotect(record.ProtectedValues);
        secrets[key] = value;
        record.SecretKeysJson = JsonSerializer.Serialize(secrets.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        record.ProtectedValues = Protect(secrets);
        record.LastModified = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("credential.secret_rotated", record, cancellationToken, new { key });
        return true;
    }

    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return false;
        db.Credentials.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("credential.deleted", record, cancellationToken);
        return true;
    }

    public async Task<string?> ResolveSecretAsync(string tenantId, string id, string key, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(tenantId, id, cancellationToken);
        if (record is null) return null;
        var normalizedKey = NormalizeRequired(key, "Key", MaxSecretKeyLength);
        var secrets = Unprotect(record.ProtectedValues);
        if (!secrets.TryGetValue(normalizedKey, out var value)) return null;
        record.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("credential.secret_resolved", record, cancellationToken, new { key = normalizedKey });
        return value;
    }

    private Task<CredentialRecord?> FindAsync(string tenantId, string id, CancellationToken cancellationToken) =>
        db.Credentials.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken);

    private Dictionary<string, string> Unprotect(string protectedValues)
    {
        try
        {
            var json = _protector.Unprotect(protectedValues);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            logger.LogError(exception, "Credential payload could not be decrypted.");
            throw new CredentialProtectionException("The credential payload could not be decrypted.", exception);
        }
    }

    private string Protect(IReadOnlyDictionary<string, string> secrets) => _protector.Protect(JsonSerializer.Serialize(secrets));

    private async Task AuditAsync(string action, CredentialRecord record, CancellationToken cancellationToken, object? details = null) =>
        await auditLogService.RecordAsync(new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            Resource = "credential",
            ResourceId = record.Id,
            TenantId = record.TenantId,
            StatusCode = 200,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details)
        }, cancellationToken);

    private static CredentialMetadata ToMetadata(CredentialRecord record) =>
        new(record.Id, record.TenantId, record.Name, record.Type, record.Description,
            JsonSerializer.Deserialize<string[]>(record.SecretKeysJson) ?? [], record.CreatedAt, record.LastModified, record.LastUsedAt);

    private static Dictionary<string, string> NormalizeSecrets(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0) throw new ArgumentException("At least one secret value is required.", nameof(values));
        if (values.Count > MaxSecretCount) throw new ArgumentException($"A credential may contain at most {MaxSecretCount} secret values.", nameof(values));
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            var key = NormalizeRequired(pair.Key, "Key", MaxSecretKeyLength);
            var value = NormalizeRequired(pair.Value, "Value", MaxSecretValueLength, trim: false);
            if (!result.TryAdd(key, value)) throw new ArgumentException($"Duplicate secret key '{key}'.", nameof(values));
        }
        return result;
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength, bool trim = true)
    {
        var normalized = trim ? value?.Trim() : value;
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException($"{fieldName} is required.", fieldName);
        if (normalized.Length > maxLength) throw new ArgumentException($"{fieldName} exceeds the maximum length of {maxLength}.", fieldName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentException($"Description exceeds the maximum length of {maxLength}.", nameof(value));
        return normalized;
    }

    private static void ValidateTenant(string tenantId) => _ = NormalizeRequired(tenantId, "TenantId", 64);
}

public sealed class CredentialConflictException(string message) : Exception(message);
public sealed class CredentialProtectionException(string message, Exception innerException) : Exception(message, innerException);
