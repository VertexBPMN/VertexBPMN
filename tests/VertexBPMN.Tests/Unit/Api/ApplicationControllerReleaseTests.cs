using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

/// <summary>LC05 API slice: release and released-revision (no draft leak) contracts.</summary>
public sealed class ApplicationControllerReleaseTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private static ApplicationController Controller(Mock<IApplicationDefinitionService> service) => new(service.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", TenantA)], "Test"))
            }
        }
    };

    // ---------------------------------------------------------------- Release
    [Fact]
    public async Task Release_ReturnsNoContent()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.ReleaseAsync(TenantA, "cand-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var response = await Controller(service).Release("cand-1", TenantA, CancellationToken.None);
        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task Release_CandidateNotFound_ReturnsNotFound()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.ReleaseAsync(TenantA, "cand-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var response = await Controller(service).Release("cand-1", TenantA, CancellationToken.None);
        Assert.IsType<NotFoundResult>(response);
    }

    [Fact]
    public async Task Release_ForeignTenant_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.ReleaseAsync(TenantA, "cand-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationForeignTenantException("Foreign tenant reference."));
        var response = await Controller(service).Release("cand-1", TenantA, CancellationToken.None);
        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task Release_InvalidRevision_Returns400()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.ReleaseAsync(TenantA, "cand-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationValidationException("Invalid revision."));
        var response = await Controller(service).Release("cand-1", TenantA, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task Release_CrossTenantRequest_Returns403WithoutServiceCall()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).Release("cand-1", TenantB, CancellationToken.None);
        Assert.IsType<ForbidResult>(response);
        service.Verify(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- Get released revision (draft must not leak)
    [Fact]
    public async Task GetReleasedRevision_ReturnsOnlyReleasedRevision_NotDraft()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var released = new ApplicationRevisionRecord
        {
            Id = "rev-1", ApplicationDefinitionId = "app-1", TenantId = TenantA, Version = 1, ContentJson = """{"released":true}"""
        };
        service.Setup(s => s.GetReleasedRevisionAsync(TenantA, "app-1", It.IsAny<CancellationToken>())).ReturnsAsync(released);
        var response = await Controller(service).GetReleasedRevision("app-1", TenantA, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(released, ok.Value);
        Assert.Equal(1, released.Version);
        // The released-revision endpoint must never fall back to reading the mutable draft or current revision.
        service.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.GetRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReleasedRevision_None_ReturnsNotFound()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.GetReleasedRevisionAsync(TenantA, "app-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationRevisionRecord?)null);
        var response = await Controller(service).GetReleasedRevision("app-1", TenantA, CancellationToken.None);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GetReleasedRevision_CrossTenantRequest_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).GetReleasedRevision("app-1", TenantB, CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
        service.Verify(s => s.GetReleasedRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
