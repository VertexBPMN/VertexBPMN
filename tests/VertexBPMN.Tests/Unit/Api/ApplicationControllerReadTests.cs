using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

/// <summary>LC05 API slice: draft/revision/release-candidate read and creation contracts.</summary>
public sealed class ApplicationControllerReadTests
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

    // ---------------------------------------------------------------- Get draft
    [Fact]
    public async Task GetDraft_ReturnsMetadata()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var draft = new ApplicationDefinitionMetadata("app-1", TenantA, "invoice", "Invoice", 2, "{}");
        service.Setup(s => s.GetAsync(TenantA, "app-1", It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        var response = await Controller(service).Get("app-1", TenantA, CancellationToken.None);
        Assert.Equal(draft, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    [Fact]
    public async Task GetDraft_Missing_ReturnsNotFound()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.GetAsync(TenantA, "app-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationDefinitionMetadata?)null);
        var response = await Controller(service).Get("app-1", TenantA, CancellationToken.None);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GetDraft_CrossTenantRequest_Returns403WithoutServiceCall()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).Get("app-1", TenantB, CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
        service.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- Get revision by version
    [Fact]
    public async Task GetRevision_ReturnsRevision()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var revision = new ApplicationRevisionRecord { Id = "rev-1", ApplicationDefinitionId = "app-1", TenantId = TenantA, Version = 1, ContentJson = "{}" };
        service.Setup(s => s.GetRevisionAsync(TenantA, "app-1", 1, It.IsAny<CancellationToken>())).ReturnsAsync(revision);
        var response = await Controller(service).GetRevision("app-1", 1, TenantA, CancellationToken.None);
        Assert.Equal(revision, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    [Fact]
    public async Task GetRevision_Missing_ReturnsNotFound()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.GetRevisionAsync(TenantA, "app-1", 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationRevisionRecord?)null);
        var response = await Controller(service).GetRevision("app-1", 9, TenantA, CancellationToken.None);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GetRevision_CrossTenantRequest_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).GetRevision("app-1", 1, TenantB, CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
        service.Verify(s => s.GetRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- Create release candidate from version
    [Fact]
    public async Task CreateReleaseCandidate_ReturnsCandidate()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var candidate = new ApplicationReleaseCandidateRecord { Id = "cand-1", ApplicationDefinitionId = "app-1", ApplicationRevisionId = "rev-1", TenantId = TenantA };
        service.Setup(s => s.CreateReleaseCandidateAsync(TenantA, "app-1", 1, It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        var response = await Controller(service).CreateReleaseCandidate("app-1", 1, TenantA, CancellationToken.None);
        Assert.Equal(candidate, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    [Fact]
    public async Task CreateReleaseCandidate_RevisionNotFound_Returns409()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.CreateReleaseCandidateAsync(TenantA, "app-1", 9, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationDefinitionConflictException("Revision not found."));
        var response = await Controller(service).CreateReleaseCandidate("app-1", 9, TenantA, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(response.Result);
    }

    [Fact]
    public async Task CreateReleaseCandidate_InvalidDraft_Returns400()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.CreateReleaseCandidateAsync(TenantA, "app-1", 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationValidationException("Invalid domain draft."));
        var response = await Controller(service).CreateReleaseCandidate("app-1", 1, TenantA, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    // ---------------------------------------------------------------- Get release candidate
    [Fact]
    public async Task GetReleaseCandidate_ReturnsCandidate()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var candidate = new ApplicationReleaseCandidateRecord { Id = "cand-1", ApplicationDefinitionId = "app-1", ApplicationRevisionId = "rev-1", TenantId = TenantA };
        service.Setup(s => s.GetReleaseCandidateAsync(TenantA, "cand-1", It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        var response = await Controller(service).GetReleaseCandidate("cand-1", TenantA, CancellationToken.None);
        Assert.Equal(candidate, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    [Fact]
    public async Task GetReleaseCandidate_CrossTenantRequest_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).GetReleaseCandidate("cand-1", TenantB, CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
        service.Verify(s => s.GetReleaseCandidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
