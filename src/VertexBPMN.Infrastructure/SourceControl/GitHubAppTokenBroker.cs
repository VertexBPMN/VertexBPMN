using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>No token cache: each operation observes key rotation and installation revocation.</summary>
internal sealed class GitHubAppTokenBroker(SourceControlCredentialResolver resolver, IOptions<SourceControlOptions> options)
{
    internal async Task<GitHubTokenLease> IssueAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> authenticatedRoles, RepositoryPermission permission, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.Limits.ReadTimeout);
        using var client = SourceControlHttps.CreateClient(options.Value.AllowedHosts, options.Value.Limits.ReadTimeout);
        return await IssueAsync(context, repositoryId, authenticatedRoles, permission, client, timeout.Token);
    }
    internal async Task<GitHubTokenLease> IssueAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> authenticatedRoles, RepositoryPermission permission,
        HttpClient client, CancellationToken cancellationToken)
    {
        var access = await resolver.AuthorizeAsync(context, repositoryId, authenticatedRoles, permission, cancellationToken);
        if (access.Binding.Remote.Host != "github.com") Fail();
        var revision = access.Revision;
        async Task<string> Resolve(string key) => await resolver.ResolveAsync(context, repositoryId, revision,
            authenticatedRoles, permission, "GitHubApp", key, cancellationToken);
        try
        {
            var appId = await Resolve("appId");
            var installation = await Resolve("installationId");
            var remoteRepository = await Resolve("repositoryId");
            _ = PositiveId(appId);
            var installationId = PositiveId(installation);
            var remoteId = PositiveId(remoteRepository);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(await Resolve("privateKey"));
            var now = DateTimeOffset.UtcNow;
            var header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var claims = Base64(JsonSerializer.SerializeToUtf8Bytes(new { iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
                exp = now.AddMinutes(8).ToUnixTimeSeconds(), iss = appId }));
            var unsigned = header + "." + claims;
            var jwt = unsigned + "." + Base64(rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.github.com/app/installations/{installationId}/access_tokens");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Headers.UserAgent.ParseAdd("VertexBPMN/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            var contentsWrite = (permission & RepositoryPermission.Push) != 0;
            var pullRequestWrite = (permission & RepositoryPermission.PullRequest) != 0;
            request.Content = JsonContent.Create(new { repository_ids = new[] { remoteId },
                permissions = new Dictionary<string, string> { ["contents"] = contentsWrite ? "write" : "read", ["pull_requests"] = pullRequestWrite ? "write" : "read" } });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) Fail(); // No raw body/provider exception escapes.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) != 0)
            {
                if (buffer.Length + read > 64 * 1024) Fail();
                buffer.Write(chunk, 0, read);
            }
            using var json = JsonDocument.Parse(buffer.ToArray());
            var token = json.RootElement.GetProperty("token").GetString();
            var expires = json.RootElement.GetProperty("expires_at").GetDateTimeOffset();
            if (string.IsNullOrEmpty(token) || token.Any(char.IsControl)
                || expires <= DateTimeOffset.UtcNow.AddMinutes(1) || expires > now.AddHours(1).AddMinutes(1)) Fail();
            // Fresh check after exchange: rotation/ACL edits during issuance invalidate this result.
            var current = await resolver.AuthorizeAsync(context, repositoryId, authenticatedRoles, permission, cancellationToken);
            if (current.Revision != revision) throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            return new GitHubTokenLease(token!, expires);
        }
        catch (OperationCanceledException) { throw; }
        catch (SourceControlSecurityException) { throw; }
        catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
    }

    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static long PositiveId(string value)
    {
        if (!long.TryParse(value, out var id) || id <= 0) Fail();
        return id;
    }
    [DoesNotReturn]
    private static void Fail() => throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
}

internal sealed class GitHubTokenLease(string token, DateTimeOffset expiresAt) : IDisposable
{
    private char[]? _token = token.ToCharArray();
    internal DateTimeOffset ExpiresAt { get; } = expiresAt;
    internal AuthenticationHeaderValue Authorization()
    {
        if (_token is null || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
        return new("Bearer", new string(_token));
    }
    public void Dispose()
    {
        if (_token is not null) Array.Clear(_token);
        _token = null;
    }
    public override string ToString() => "GitHub installation token (redacted)";
}
