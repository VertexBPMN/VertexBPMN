using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class TaskCompletionConflictTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task DifferentAssignee_CannotCompleteTask()
    {
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a", Assignee = "another-user" };
        var service = new Mock<ITaskService>();
        service.Setup(s => s.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("tenant_id", "tenant-a"), new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"))
            } }
        };
        var response = await controller.Complete(task.Id, new TaskController.CompleteRequest(null));
        Assert.IsType<ForbidResult>(response);
        service.Verify(s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Completion_WithOidcToken_ResolvesSubjectFromSubClaim()
    {
        // MapInboundClaims=false: real OIDC tokens expose 'sub', not ClaimTypes.NameIdentifier.
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a", Revision = 7,
            FormSchema = """{"components":[]}""", FormSchemaHash = "hash-1", Assignee = "oidc-subject-42" };
        var service = new Mock<ITaskService>();
        var completion = service.As<ITaskCompletionService>();
        service.Setup(s => s.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            IdempotencyKey = "attempt-oidc",
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("tenant_id", "tenant-a"),
                    new Claim("sub", "oidc-subject-42"),
                    new Claim("preferred_username", "yova@example.com")], "Bearer"))
            } }
        };
        var response = await controller.Complete(task.Id, new TaskController.CompleteRequest(null, "tenant-a", 7, "hash-1"));
        Assert.IsType<NoContentResult>(response);
        completion.Verify(s => s.CompleteValidatedAsync(task.Id, null, "attempt-oidc", 7, "hash-1", "oidc-subject-42", "tenant-a",
            false, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public async Task Completion_UsesSharedValidatedContract(long persistedRevision)
    {
        // A replay carries its original revision; the runtime owns replay detection.
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a", Revision = persistedRevision,
            FormSchema = """{"components":[]}""", FormSchemaHash = "hash-1" };
        var service = new Mock<ITaskService>();
        var completion = service.As<ITaskCompletionService>();
        service.Setup(s => s.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            IdempotencyKey = "attempt-1",
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("tenant_id", "tenant-a"), new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"))
            } }
        };
        var response = await controller.Complete(task.Id, new TaskController.CompleteRequest(null, "tenant-a", 7, "hash-1"));
        Assert.IsType<NoContentResult>(response);
        completion.Verify(s => s.CompleteValidatedAsync(task.Id, null, "attempt-1", 7, "hash-1", "user-a", "tenant-a",
            false, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(6, "hash-1")]
    [InlineData(7, "hash-stale")]
    public async Task StaleSnapshot_ReturnsConflict(long revision, string hash)
    {
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a", Revision = 7,
            FormSchema = """{"components":[]}""", FormSchemaHash = "hash-1" };
        var service = new Mock<ITaskService>();
        service.Setup(s => s.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            IdempotencyKey = "attempt-1",
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("tenant_id", "tenant-a"), new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"))
            } }
        };
        var json = JsonSerializer.Serialize(new { variables = new Dictionary<string, object>(StringComparer.Ordinal),
            expectedRevision = revision, formSchemaHash = hash });
        var request = JsonSerializer.Deserialize<TaskController.CompleteRequest>(json, JsonOptions)!;

        var response = await controller.Complete(task.Id, request);

        Assert.IsType<ConflictObjectResult>(response);
        service.Verify(s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
