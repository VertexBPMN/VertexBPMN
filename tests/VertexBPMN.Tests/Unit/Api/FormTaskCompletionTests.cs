using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class FormTaskCompletionTests
{
    [Fact]
    public async Task MissingRequiredFormField_DoesNotCallCompletion()
    {
        var task = new UserTask
        {
            Id = Guid.NewGuid(), TenantId = "tenant-a",
            FormKey = "approval",
            FormSchema = """{"type":"default","components":[{"type":"textfield","key":"reason","validate":{"required":true}}]}"""
        };
        var service = new Mock<ITaskService>();
        service.Setup(s => s.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("tenant_id", "tenant-a"), new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"))
                }
            }
        };

        var result = await controller.Complete(task.Id, new TaskController.CompleteRequest(new Dictionary<string, object>(StringComparer.Ordinal)));

        Assert.IsType<BadRequestObjectResult>(result);
        service.Verify(s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
