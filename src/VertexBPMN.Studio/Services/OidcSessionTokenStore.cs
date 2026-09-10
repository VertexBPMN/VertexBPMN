using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Studio.Services;

public sealed class OidcSessionTokenStore(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    TimeProvider timeProvider,
    ILogger<OidcSessionTokenStore> logger)
{
    public const string SessionIdClaim = "vertexbpmn_session_id";
    public const string SessionRenewedItem = "VertexBPMN.OidcSessionRenewed";
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);

    public void Register(ClaimsPrincipal principal, AuthenticationProperties properties)
    {
        var sessionId = GetSessionId(principal);
        var tokenSet = ReadTokenSet(principal, properties, revision: 0);
        _sessions.AddOrUpdate(
            sessionId,
            _ => new SessionEntry(tokenSet),
            (_, existing) =>
            {
                existing.State = tokenSet with { Revision = existing.State.Revision + 1 };
                return existing;
            });
    }

    public async Task<string> GetAccessTokenAsync(
        ClaimsPrincipal principal,
        AuthenticationProperties? fallbackProperties,
        CancellationToken cancellationToken)
    {
        var sessionId = GetSessionId(principal);
        var entry = GetOrRegister(sessionId, principal, fallbackProperties);
        var state = await EnsureFreshAsync(sessionId, entry, cancellationToken);
        return state.AccessToken;
    }

    public async Task<bool> SynchronizeTicketAsync(
        ClaimsPrincipal principal,
        AuthenticationProperties properties,
        CancellationToken cancellationToken)
    {
        var sessionId = GetSessionId(principal);
        var entry = GetOrRegister(sessionId, principal, properties);
        var state = await EnsureFreshAsync(sessionId, entry, cancellationToken);
        var ticketAccessToken = properties.GetTokenValue("access_token");

        if (string.Equals(ticketAccessToken, state.AccessToken, StringComparison.Ordinal))
            return false;

        ApplyTokenSet(properties, state);
        return true;
    }

    public ClaimsPrincipal GetCurrentPrincipal(ClaimsPrincipal fallback)
    {
        var sessionId = GetSessionId(fallback);
        return _sessions.TryGetValue(sessionId, out var entry)
            ? ClonePrincipal(entry.State.Principal)
            : fallback;
    }

    public string? GetIdToken(ClaimsPrincipal principal)
    {
        var sessionId = GetSessionId(principal);
        return _sessions.TryGetValue(sessionId, out var entry) ? entry.State.IdToken : null;
    }

    public void Remove(ClaimsPrincipal principal)
    {
        var sessionId = principal.FindFirstValue(SessionIdClaim);
        if (!string.IsNullOrWhiteSpace(sessionId))
            _sessions.TryRemove(sessionId, out _);
    }

    private SessionEntry GetOrRegister(
        string sessionId,
        ClaimsPrincipal principal,
        AuthenticationProperties? properties)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
            return existing;
        if (properties is null)
            throw new OidcSessionExpiredException("The OIDC session is no longer available. Sign in again.");

        var created = new SessionEntry(ReadTokenSet(principal, properties, revision: 0));
        return _sessions.GetOrAdd(sessionId, created);
    }

    private async Task<SessionTokenSet> EnsureFreshAsync(
        string sessionId,
        SessionEntry entry,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (entry.State.ExpiresAt > now.Add(RefreshWindow))
            return entry.State;

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (entry.State.ExpiresAt > now.Add(RefreshWindow))
                return entry.State;

            var refreshed = await RefreshAsync(entry.State, cancellationToken);
            entry.State = refreshed with { Revision = entry.State.Revision + 1 };
            return entry.State;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _sessions.TryRemove(sessionId, out _);
            logger.LogWarning(exception, "OIDC token refresh failed for session {SessionId}.", sessionId);
            throw new OidcSessionExpiredException("The OIDC session could not be refreshed. Sign in again.", exception);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private async Task<SessionTokenSet> RefreshAsync(
        SessionTokenSet current,
        CancellationToken cancellationToken)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = options.Configuration;
        if (configuration is null)
        {
            if (options.ConfigurationManager is null)
                throw new InvalidOperationException("OIDC discovery is unavailable.");
            configuration = await options.ConfigurationManager.GetConfigurationAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(configuration.TokenEndpoint))
            throw new InvalidOperationException("OIDC discovery does not provide a token endpoint.");
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            throw new InvalidOperationException("The confidential OIDC client is not completely configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = current.RefreshToken,
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret
            })
        };
        using var response = await options.Backchannel.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OIDC refresh was rejected with HTTP {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var accessToken = RequiredString(root, "access_token");
        var refreshToken = OptionalString(root, "refresh_token") ?? current.RefreshToken;
        var idToken = OptionalString(root, "id_token") ?? current.IdToken;
        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
            && expiresElement.TryGetInt32(out var seconds)
            && seconds > 0
                ? seconds
                : throw new InvalidOperationException("OIDC refresh response has no valid expires_in value.");

        var accessPrincipal = await ValidateTokenAsync(
            accessToken,
            options,
            configuration,
            "vertexbpmn-api",
            cancellationToken);
        if (!VertexOidcClaims.TryNormalize(accessPrincipal, out var claimsError))
            throw new SecurityTokenValidationException(claimsError);
        EnsureSameSubject(current.Principal, accessPrincipal);
        PreserveSessionId(current.Principal, accessPrincipal);

        if (!string.IsNullOrWhiteSpace(idToken))
        {
            var idPrincipal = await ValidateTokenAsync(
                idToken,
                options,
                configuration,
                options.ClientId,
                cancellationToken);
            EnsureSameSubject(current.Principal, idPrincipal);
        }

        return new SessionTokenSet(
            accessToken,
            refreshToken,
            idToken,
            timeProvider.GetUtcNow().AddSeconds(expiresIn),
            ClonePrincipal(accessPrincipal),
            current.Revision);
    }

    private static async Task<ClaimsPrincipal> ValidateTokenAsync(
        string token,
        OpenIdConnectOptions options,
        OpenIdConnectConfiguration configuration,
        string audience,
        CancellationToken cancellationToken)
    {
        var validation = options.TokenValidationParameters.Clone();
        validation.ValidateIssuer = true;
        validation.ValidIssuer = configuration.Issuer;
        validation.ValidateAudience = true;
        validation.ValidAudience = audience;
        validation.ValidateLifetime = true;
        validation.ValidateIssuerSigningKey = true;
        validation.IssuerSigningKeys = configuration.SigningKeys;
        validation.ClockSkew = TimeSpan.FromSeconds(30);

        var result = await options.TokenHandler.ValidateTokenAsync(token, validation);
        if (!result.IsValid || result.ClaimsIdentity is null)
            throw result.Exception ?? new SecurityTokenValidationException("OIDC token validation failed.");

        cancellationToken.ThrowIfCancellationRequested();
        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    private static void EnsureSameSubject(ClaimsPrincipal expected, ClaimsPrincipal actual)
    {
        var expectedSubject = expected.FindFirstValue("sub");
        var actualSubject = actual.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(expectedSubject)
            || !string.Equals(expectedSubject, actualSubject, StringComparison.Ordinal))
        {
            throw new SecurityTokenValidationException("The refreshed token belongs to a different subject.");
        }
    }

    private static void PreserveSessionId(ClaimsPrincipal current, ClaimsPrincipal refreshed)
    {
        var sessionId = GetSessionId(current);
        var identity = refreshed.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated)
            ?? throw new SecurityTokenValidationException("The refreshed token has no authenticated identity.");
        identity.AddClaim(new Claim(SessionIdClaim, sessionId));
    }

    private static SessionTokenSet ReadTokenSet(
        ClaimsPrincipal principal,
        AuthenticationProperties properties,
        long revision)
    {
        var accessToken = properties.GetTokenValue("access_token");
        var refreshToken = properties.GetTokenValue("refresh_token");
        var expiresAtText = properties.GetTokenValue("expires_at");
        if (string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(refreshToken)
            || !DateTimeOffset.TryParse(
                expiresAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt))
        {
            throw new OidcSessionExpiredException("The authentication ticket has no refreshable OIDC token set.");
        }

        return new SessionTokenSet(
            accessToken,
            refreshToken,
            properties.GetTokenValue("id_token"),
            expiresAt,
            ClonePrincipal(principal),
            revision);
    }

    private static void ApplyTokenSet(AuthenticationProperties properties, SessionTokenSet state)
    {
        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "access_token", Value = state.AccessToken },
            new() { Name = "refresh_token", Value = state.RefreshToken },
            new() { Name = "expires_at", Value = state.ExpiresAt.ToString("O", CultureInfo.InvariantCulture) },
            new() { Name = "token_type", Value = "Bearer" }
        };
        if (!string.IsNullOrWhiteSpace(state.IdToken))
            tokens.Add(new AuthenticationToken { Name = "id_token", Value = state.IdToken });
        properties.StoreTokens(tokens);
    }

    private static string GetSessionId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(SessionIdClaim) is { Length: > 0 } sessionId
            ? sessionId
            : throw new OidcSessionExpiredException("The authenticated identity has no OIDC session identifier.");

    private static string RequiredString(JsonElement root, string propertyName) =>
        OptionalString(root, propertyName)
        ?? throw new InvalidOperationException($"OIDC refresh response has no {propertyName} value.");

    private static string? OptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static ClaimsPrincipal ClonePrincipal(ClaimsPrincipal principal) =>
        new(principal.Identities.Select(identity => new ClaimsIdentity(identity)));

    private sealed class SessionEntry(SessionTokenSet state)
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public SessionTokenSet State { get; set; } = state;
    }

    private sealed record SessionTokenSet(
        string AccessToken,
        string RefreshToken,
        string? IdToken,
        DateTimeOffset ExpiresAt,
        ClaimsPrincipal Principal,
        long Revision);
}

public sealed class OidcSessionExpiredException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
