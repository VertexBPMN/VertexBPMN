using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

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
        CancellationToken cancellationToken = default)
    {
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
        await db.SaveChangesAsync(cancellationToken);

        var redirectUrl =
            $"{config.AuthorizationUrl}" +
            $"?response_type=code" +
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
        CancellationToken cancellationToken = default)
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

        var clientSecret = await credentialService.ResolveSecretAsync(
            record.TenantId, record.CredentialId, "client_secret", cancellationToken);
        if (clientSecret is null)
        {
            Logger().LogWarning("OAuth2 credential {CredentialId} has no client_secret", record.CredentialId);
            return false;
        }

        var http = httpClientFactory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = record.RedirectUri,
            ["client_id"] = record.ClientId,
            ["client_secret"] = clientSecret
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(record.TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        }
        catch (Exception exception)
        {
            Logger().LogWarning(exception, "OAuth2 token request failed for credential {CredentialId}", record.CredentialId);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger().LogWarning("OAuth2 token endpoint returned {Status} for credential {CredentialId}",
                (int)response.StatusCode, record.CredentialId);
            return false;
        }

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
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

        db.OAuth2FlowStates.Remove(record);
        await db.SaveChangesAsync(cancellationToken);

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

        var http = httpClientFactory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form), cancellationToken);
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
