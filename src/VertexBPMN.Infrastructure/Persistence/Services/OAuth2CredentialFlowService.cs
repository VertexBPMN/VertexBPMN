using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// OAuth2-Authorization-Code-Flow (RFC 6749 §4.1).
/// Persistente Metadaten in <see cref="OAuth2FlowStateRecord"/> (tenant-isoliert),
/// Tokens als verschlüsselte Secrets im Credential-Store ("access_token", "refresh_token",
/// "expires_at", "token_url", "client_id", "client_secret").
/// </summary>
public sealed class OAuth2CredentialFlowService(
    BpmnDbContext db,
    ICredentialService credentialService,
    IHttpClientFactory httpClientFactory,
    IAuditLogService auditLogService,
    ILogger<OAuth2CredentialFlowService> logger) : IOAuth2CredentialFlowService
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccessTokenSkew = TimeSpan.FromSeconds(60);

    public async Task<OAuth2AuthorizationStart> StartAuthorizationAsync(
        string tenantId,
        string credentialId,
        OAuth2AuthorizationConfig config,
        CancellationToken cancellationToken = default,
        OAuth2FlowBinding? binding = null)
    {
        if (!Uri.TryCreate(config.AuthorizationUrl, UriKind.Absolute, out var authorizationUri)
            || authorizationUri.Scheme != "https" || !string.IsNullOrEmpty(authorizationUri.Fragment))
            throw new ArgumentException("OAuth2 authorization URL must be an absolute HTTPS URL without a fragment.");
        var credential = await credentialService.GetAsync(tenantId, credentialId, cancellationToken);
        if (credential is null)
            throw new ArgumentException("The credential does not exist.");

        var now = DateTime.UtcNow;
        await PruneExpiredAsync(tenantId, now, cancellationToken);

        string state;
        bool exists;
        do
        {
            state = CreateState();
            exists = await db.OAuth2FlowStates.AnyAsync(
                s => s.TenantId == tenantId && s.State == state, cancellationToken);
        } while (exists);

        db.OAuth2FlowStates.Add(new OAuth2FlowStateRecord
        {
            State = state,
            TenantId = tenantId,
            CredentialId = credentialId,
            AuthorizationUrl = config.AuthorizationUrl,
            TokenUrl = config.TokenUrl,
            ClientId = config.ClientId,
            RedirectUri = config.RedirectUri,
            Scopes = config.Scopes,
            CreatedAt = now,
            ExpiresAt = now.Add(StateTtl)
        });
        if (binding is not null)
        {
            if (string.IsNullOrWhiteSpace(binding.Subject) || binding.BrowserProof.Length is < 43 or > 256)
                throw new ArgumentException("OAuth2 requires an authenticated subject and a per-browser proof.");
            db.RuntimeInbox.Add(new RuntimeInboxMessage
            {
                Id = Guid.NewGuid(), Operation = "oauth2-browser-binding", IdempotencyKey = state,
                TenantId = tenantId, TenantScope = tenantId, ReceivedAt = now,
                Result = JsonSerializer.Serialize(new OAuth2FlowBinding(binding.Subject, HashProof(binding.BrowserProof)))
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        var redirectUrl =
            $"{config.AuthorizationUrl}" +
            $"{(config.AuthorizationUrl.Contains('?') ? "&" : "?")}response_type=code" +
            $"&client_id={Uri.EscapeDataString(config.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(config.Scopes)}" +
            $"&state={Uri.EscapeDataString(state)}";

        Logger().LogInformation("OAuth2 state issued for credential {CredentialId} in tenant {TenantId}", credentialId, tenantId);
        return new OAuth2AuthorizationStart(redirectUrl, state);
    }

    public async Task<bool> CompleteAuthorizationAsync(
        string state,
        string code,
        CancellationToken cancellationToken = default,
        OAuth2FlowBinding? binding = null)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
            return false;

        var record = await db.OAuth2FlowStates.FirstOrDefaultAsync(s => s.State == state, cancellationToken);
        if (record is null || record.ExpiresAt <= DateTime.UtcNow)
        {
            if (record is not null)
            {
                db.OAuth2FlowStates.Remove(record);
                await db.SaveChangesAsync(cancellationToken);
            }

            Logger().LogWarning("OAuth2 callback with invalid or expired state");
            return false;
        }

        var browserRecord = await db.RuntimeInbox.SingleOrDefaultAsync(x =>
            x.Operation == "oauth2-browser-binding" && x.IdempotencyKey == state && x.TenantId == record.TenantId, cancellationToken);
        var expectedBinding = browserRecord?.Result is { } serialized ? JsonSerializer.Deserialize<OAuth2FlowBinding>(serialized) : null;
        if (expectedBinding is not null || binding is not null)
        {
            if (expectedBinding is null || binding is null || !string.Equals(expectedBinding.Subject, binding.Subject, StringComparison.Ordinal)
                || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedBinding.BrowserProof),
                    Convert.FromHexString(HashProof(binding.BrowserProof)))) return false;
        }

        var clientSecret = await credentialService.ResolveSecretAsync(
            record.TenantId, record.CredentialId, "client_secret", cancellationToken);
        if (clientSecret is null)
        {
            Logger().LogWarning("OAuth2 credential {CredentialId} has no client_secret", record.CredentialId);
            return false;
        }

        var http = httpClientFactory.CreateClient("VertexBPMN.PublicEndpoints");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = record.RedirectUri,
            ["client_id"] = record.ClientId,
            ["client_secret"] = clientSecret
        };

        var tokenEndpoint = await TryGetPublicEndpointAsync(record.TokenUrl, cancellationToken);
        if (tokenEndpoint is null)
        {
            Logger().LogWarning("OAuth2 token URL for credential {CredentialId} is not a valid public destination", record.CredentialId);
            return false;
        }

        // Consume before contacting the provider. EF checks affected rows on DELETE:
        // concurrent callbacks cannot both win, across API replicas as well.
        db.OAuth2FlowStates.Remove(record);
        if (browserRecord is not null) db.RuntimeInbox.Remove(browserRecord);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return false; }

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        }
        catch (Exception exception)
        {
            Logger().LogWarning(exception, "OAuth2 token request failed for credential {CredentialId}", record.CredentialId);
            return false;
        }

        using var responseLifetime = response;
        if (!response.IsSuccessStatusCode)
        {
            Logger().LogWarning("OAuth2 token endpoint returned {Status} for credential {CredentialId}",
                (int)response.StatusCode, record.CredentialId);
            return false;
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number
            ? ei.GetInt32()
            : 3600;
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture);

        await RotateAsync(record.TenantId, record.CredentialId, "access_token", accessToken, cancellationToken);
        await RotateAsync(record.TenantId, record.CredentialId, "expires_at", expiresAt, cancellationToken);
        // token_url + client_id werden für den Refresh-Flow dauerhaft im Credential-Store abgelegt.
        await RotateAsync(record.TenantId, record.CredentialId, "token_url", record.TokenUrl, cancellationToken);
        await RotateAsync(record.TenantId, record.CredentialId, "client_id", record.ClientId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await RotateAsync(record.TenantId, record.CredentialId, "refresh_token", refreshToken, cancellationToken);

        await auditLogService.RecordAsync(new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = "credential.oauth2_completed",
            Resource = "credential",
            ResourceId = record.CredentialId,
            TenantId = record.TenantId,
            StatusCode = 200
        }, cancellationToken);

        Logger().LogInformation("OAuth2 token stored for credential {CredentialId}", record.CredentialId);
        return true;
    }

    private static string HashProof(string proof) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(proof)));

    public async Task<string?> ResolveValidAccessTokenAsync(
        string tenantId,
        string credentialId,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await credentialService.ResolveSecretAsync(tenantId, credentialId, "access_token", cancellationToken);
        var expiresAtRaw = await credentialService.ResolveSecretAsync(tenantId, credentialId, "expires_at", cancellationToken);
        if (accessToken is not null
            && DateTime.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            && expiresAt > DateTime.UtcNow.Add(AccessTokenSkew))
        {
            return accessToken;
        }

        var refreshToken = await credentialService.ResolveSecretAsync(tenantId, credentialId, "refresh_token", cancellationToken);
        var tokenUrl = await credentialService.ResolveSecretAsync(tenantId, credentialId, "token_url", cancellationToken);
        if (refreshToken is null || tokenUrl is null)
            return null;

        var clientId = await credentialService.ResolveSecretAsync(tenantId, credentialId, "client_id", cancellationToken);
        var clientSecret = await credentialService.ResolveSecretAsync(tenantId, credentialId, "client_secret", cancellationToken);
        if (clientId is null || clientSecret is null)
            return null;

        var http = httpClientFactory.CreateClient("VertexBPMN.PublicEndpoints");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        var refreshEndpoint = await TryGetPublicEndpointAsync(tokenUrl, cancellationToken);
        if (refreshEndpoint is null)
        {
            Logger().LogWarning("OAuth2 token URL for credential {CredentialId} is not a valid public destination", credentialId);
            return null;
        }

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(refreshEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        }
        catch (Exception exception)
        {
            Logger().LogWarning(exception, "OAuth2 refresh failed for credential {CredentialId}", credentialId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger().LogWarning("OAuth2 refresh endpoint returned {Status} for credential {CredentialId}",
                (int)response.StatusCode, credentialId);
            return null;
        }

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var newAccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(newAccessToken))
            return null;

        var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number
            ? ei.GetInt32()
            : 3600;
        var newExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture);

        await RotateAsync(tenantId, credentialId, "access_token", newAccessToken, cancellationToken);
        await RotateAsync(tenantId, credentialId, "expires_at", newExpiresAt, cancellationToken);
        if (!string.IsNullOrWhiteSpace(newRefreshToken))
            await RotateAsync(tenantId, credentialId, "refresh_token", newRefreshToken, cancellationToken);

        Logger().LogInformation("OAuth2 access token refreshed for credential {CredentialId}", credentialId);
        return newAccessToken;
    }

    private static async Task<Uri?> TryGetPublicEndpointAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo))
            return null;
        try
        {
            await ConnectorDestinationPolicy.ThrowIfForbiddenAsync(endpoint.Host, cancellationToken, rejectUnresolvable: false);
            return endpoint;
        }
        catch (ServiceTaskExecutionException)
        {
            return null;
        }
    }

    private async Task RotateAsync(
        string tenantId,
        string credentialId,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await credentialService.RotateSecretAsync(
            tenantId,
            credentialId,
            new CredentialSecretRotation(key, value),
            cancellationToken);
    }

    private async Task PruneExpiredAsync(string tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var expired = await db.OAuth2FlowStates
            .Where(s => s.TenantId == tenantId && s.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return;

        db.OAuth2FlowStates.RemoveRange(expired);
        var expiredStates = expired.Select(x => x.State).ToArray();
        var bindings = await db.RuntimeInbox.Where(x => x.Operation == "oauth2-browser-binding"
            && x.TenantId == tenantId && expiredStates.Contains(x.IdempotencyKey)).ToListAsync(cancellationToken);
        db.RuntimeInbox.RemoveRange(bindings);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string CreateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private ILogger<OAuth2CredentialFlowService> Logger() => logger;
}
