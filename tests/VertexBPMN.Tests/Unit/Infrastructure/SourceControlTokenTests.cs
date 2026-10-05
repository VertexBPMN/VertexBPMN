using System.Net;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class SourceControlTokenTests
{
    [Theory]
    [InlineData(-1, "expired")]
    [InlineData(120, "too-long-lived")]
    [InlineData(30, "injected\nheader")]
    public async Task Unsafe_or_expired_provider_tokens_are_rejected(int minutes, string value)
    {
        using var rsa = RSA.Create(2048);
        var (broker, context, binding) = Broker(rsa.ExportRSAPrivateKeyPem());
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                { token = value, expires_at = DateTimeOffset.UtcNow.AddMinutes(minutes) }))
        }));
        Assert.Equal(SourceControlErrorCode.CredentialUnavailable, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            broker.IssueAsync(context, binding.Id, ["Admin"], RepositoryPermission.Push, client, TestContext.Current.CancellationToken))).Code);
    }
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task Token_failure_is_redacted_and_not_retried(HttpStatusCode status)
    {
        using var rsa = RSA.Create(2048);
        var (broker, context, binding) = Broker(rsa.ExportRSAPrivateKeyPem());
        var calls = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            calls++;
            return new HttpResponseMessage(status) { Content = new StringContent("test-provider-secret") };
        }));
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => broker.IssueAsync(context,
            binding.Id, ["Admin"], RepositoryPermission.Push, client, TestContext.Current.CancellationToken));
        Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
        Assert.DoesNotContain("test-provider-secret", error.ToString());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Installation_token_is_repository_scoped_expiring_and_diagnostics_safe()
    {
        using var rsa = RSA.Create(2048);
        var (broker, context, binding) = Broker(rsa.ExportRSAPrivateKeyPem());
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/app/installations/123/access_tokens", request.RequestUri!.AbsoluteUri);
            Assert.DoesNotContain("private", request.Headers.Authorization!.Parameter!);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(
                $"{{\"token\":\"test-installation-token\",\"expires_at\":\"{DateTimeOffset.UtcNow.AddMinutes(50):O}\"}}") };
        }));
        using var token = await broker.IssueAsync(context, binding.Id, ["Admin"], RepositoryPermission.Push,
            client, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("test-installation-token", token.ToString());
        Assert.Equal("test-installation-token", token.Authorization().Parameter);
        token.Dispose();
        Assert.Throws<SourceControlSecurityException>(() => token.Authorization());
    }

    private static (GitHubAppTokenBroker, SourceControlContext, RepositoryBinding) Broker(string privateKey)
    {
        var context = new SourceControlContext("tenant-a", "issuer|alice");
        var binding = new RepositoryBinding(Guid.NewGuid(), context.TenantId, new("https://github.com/example/model.git"),
            "credential-id", "master", "release", ["models"]);
        var access = new Mock<ISourceControlAccessStore>();
        access.Setup(x => x.FindAsync(context.TenantId, binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryAccessSnapshot(binding, 1, [new(context.ActorId, RepositoryPermission.Read | RepositoryPermission.Push)]));
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(x => x.GetAsync(context.TenantId, binding.CredentialReference!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialMetadata("credential-id", context.TenantId, "Git", "GitHubApp", null,
                ["appId", "installationId", "repositoryId", "privateKey"], DateTime.UtcNow, DateTime.UtcNow, null));
        var values = new Dictionary<string, string> { ["appId"] = "42", ["installationId"] = "123", ["repositoryId"] = "456", ["privateKey"] = privateKey };
        credentials.Setup(x => x.ResolveSecretAsync(context.TenantId, binding.CredentialReference!, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string tenant, string id, string key, CancellationToken cancellation) => values[key]);
        return (new(new SourceControlCredentialResolver(access.Object, credentials.Object), Options.Create(new SourceControlOptions())), context, binding);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("\"repository_ids\":[456]", body);
            Assert.Contains("\"contents\":\"write\"", body);
            Assert.Contains("\"pull_requests\":\"read\"", body);
            Assert.DoesNotContain("privateKey", body);
            return respond(request);
        }
    }
}
