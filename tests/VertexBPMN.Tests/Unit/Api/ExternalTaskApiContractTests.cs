using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Api.Security;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class ExternalTaskApiContractTests
{
    [Fact]
    public async Task ClaimDerivesWorkerIdentityFromExactClaims()
    {
        var service = new Mock<IExternalTaskLeaseService>(MockBehavior.Strict);
        ExternalTaskWorkerContext? capturedWorker = null;
        service.Setup(item => item.ClaimAsync(It.IsAny<ExternalTaskWorkerContext>(),
                It.IsAny<ExternalTaskClaimCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ExternalTaskWorkerContext worker, ExternalTaskClaimCommand _, CancellationToken _) => capturedWorker = worker)
            .ReturnsAsync([]);
        var controller = Controller(service.Object, Principal(
            new Claim("iss", "https://issuer"), new Claim("sub", "worker-1"),
            new Claim("tenant_id", "tenant-a"), new Claim("external_task_topic", "documents"),
            new Claim("external_task_profile", "extractor")));

        var result = await controller.Claim(
            new ExternalTaskController.ExternalTaskClaimRequest(["documents"]),
            TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(capturedWorker);
        Assert.Equal("https://issuer", capturedWorker.Issuer);
        Assert.Equal("worker-1", capturedWorker.Subject);
        Assert.Equal("tenant-a", capturedWorker.TenantId);
        Assert.Contains("documents", capturedWorker.Topics);
        Assert.Contains("extractor", capturedWorker.Profiles);
        service.VerifyAll();
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("tenant")]
    [InlineData("topic")]
    [InlineData("wildcard")]
    public async Task ClaimRejectsMissingAmbiguousOrWildcardAuthorityBeforeService(string defect)
    {
        var claims = new List<Claim>
        {
            new("iss", "https://issuer"), new("sub", "worker-1"),
            new("tenant_id", "tenant-a"), new("external_task_topic", "documents")
        };
        var targetType = defect switch
        {
            "issuer" => "iss", "subject" => "sub", "tenant" => "tenant_id", _ => "external_task_topic"
        };
        if (defect != "wildcard")
            claims.RemoveAll(item => item.Type == targetType);
        if (defect == "issuer") claims.AddRange([new("iss", "one"), new("iss", "two")]);
        if (defect == "wildcard")
        {
            claims.RemoveAll(item => item.Type == targetType);
            claims.Add(new Claim(targetType, "*"));
        }
        var service = new Mock<IExternalTaskLeaseService>(MockBehavior.Strict);
        var controller = Controller(service.Object, Principal([.. claims]));

        var result = await controller.Claim(
            new ExternalTaskController.ExternalTaskClaimRequest(["documents"]),
            TestContext.Current.CancellationToken);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(forbidden.Value);
        Assert.Equal("worker_forbidden", problem.Extensions["code"]);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LeaseErrorsMapToStableProblemDetailsWithoutExceptionText()
    {
        var service = new Mock<IExternalTaskLeaseService>(MockBehavior.Strict);
        service.Setup(item => item.HeartbeatAsync(It.IsAny<ExternalTaskWorkerContext>(), It.IsAny<Guid>(),
                It.IsAny<ExternalTaskHeartbeatCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalTaskLeaseException("lease_lost"));
        var controller = Controller(service.Object, Principal(
            new Claim("iss", "issuer"), new Claim("sub", "worker"), new Claim("tenant_id", "tenant"),
            new Claim("external_task_topic", "documents")));

        var result = await controller.Heartbeat(Guid.NewGuid(),
            new ExternalTaskController.ExternalTaskHeartbeatRequest(Guid.NewGuid(), 1),
            TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("lease_lost", problem.Extensions["code"]);
        Assert.Equal("trace-a03", problem.Extensions["traceId"]);
        Assert.Null(problem.Detail);
        service.VerifyAll();
    }

    [Fact]
    public void RequestContractsRejectUnknownJsonMembers()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskClaimRequest>(
            "{\"topics\":[\"documents\"],\"tenantId\":\"forged\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskHeartbeatRequest>(
            "{\"leaseId\":\"00000000-0000-0000-0000-000000000001\",\"leaseGeneration\":1,\"workerId\":\"forged\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskCompleteRequest>(
            "{\"leaseId\":\"00000000-0000-0000-0000-000000000001\",\"leaseGeneration\":1," +
            "\"completionId\":\"00000000-0000-0000-0000-000000000002\",\"result\":{},\"tenantId\":\"forged\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskFailRequest>(
            "{\"leaseId\":\"00000000-0000-0000-0000-000000000001\",\"leaseGeneration\":1," +
            "\"failureId\":\"00000000-0000-0000-0000-000000000002\",\"kind\":\"technical\"," +
            "\"code\":\"transport_failure\",\"subject\":\"forged\"}", options));
    }

    [Theory]
    [InlineData("payload_too_large", StatusCodes.Status413PayloadTooLarge)]
    [InlineData("result_schema_invalid", StatusCodes.Status422UnprocessableEntity)]
    [InlineData("business_error_not_allowed", StatusCodes.Status422UnprocessableEntity)]
    [InlineData("completion_conflict", StatusCodes.Status409Conflict)]
    public async Task CompletionErrorsUseStableHttpContract(string code, int expectedStatus)
    {
        var service = new Mock<IExternalTaskLeaseService>(MockBehavior.Strict);
        service.Setup(item => item.CompleteAsync(It.IsAny<ExternalTaskWorkerContext>(), It.IsAny<Guid>(),
                It.IsAny<ExternalTaskCompleteCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalTaskLeaseException(code));
        var controller = Controller(service.Object, Principal(
            new Claim("iss", "issuer"), new Claim("sub", "worker"), new Claim("tenant_id", "tenant"),
            new Claim("external_task_topic", "documents")));
        using var json = JsonDocument.Parse("{}");

        var result = await controller.Complete(Guid.NewGuid(),
            new ExternalTaskController.ExternalTaskCompleteRequest(Guid.NewGuid(), 1, Guid.NewGuid(),
                json.RootElement.Clone()), TestContext.Current.CancellationToken);

        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(expectedStatus, problemResult.StatusCode);
        Assert.Equal(code, Assert.IsType<ProblemDetails>(problemResult.Value).Extensions["code"]);
        service.VerifyAll();
    }

    [Fact]
    public async Task WorkerPolicyRequiresBearerWorkerRoleAndWorkerClaims()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Development", ["Jwt:Audience"] = "vertex",
            ["Jwt:SecretKey"] = "this-is-a-development-signing-key-long-enough"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProductionSecurity(configuration);
        await using var provider = services.BuildServiceProvider();
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("ExternalTaskWorker");

        Assert.NotNull(policy);
        Assert.Equal(["Bearer"], policy.AuthenticationSchemes);
        Assert.Contains(policy.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement roles
            && roles.AllowedRoles.Contains("ExternalTaskWorker"));
        var claims = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.ClaimsAuthorizationRequirement>()
            .Select(requirement => requirement.ClaimType).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(VertexOidcClaims.SubjectClaimType, claims);
        Assert.Contains(VertexOidcClaims.TenantClaimType, claims);
        Assert.Contains(VertexOidcClaims.ExternalTaskTopicClaimType, claims);
    }

    private static ExternalTaskController Controller(IExternalTaskLeaseService service, ClaimsPrincipal principal)
        => new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal, TraceIdentifier = "trace-a03" }
            }
        };

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role));
}
