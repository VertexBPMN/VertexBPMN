using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

/// <summary>LC05 API slice: draft create/update (expectedVersion concurrency) contracts.</summary>
public sealed class ApplicationControllerWriteTests
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

    // ---------------------------------------------------------------- Create draft
    [Fact]
    public async Task CreateDraft_ReturnsCreatedWithMetadata()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var created = new ApplicationDefinitionMetadata("app-1", TenantA, "invoice", "Invoice", 1, "{}");
        service.Setup(s => s.CreateAsync(TenantA, It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        var response = await Controller(service).Create(new(TenantA, "invoice", "Invoice", 0, "{}"), CancellationToken.None);
        Assert.Equal(created, Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    [Fact]
    public async Task CreateDraft_Conflict_Returns409()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.CreateAsync(TenantA, It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationDefinitionConflictException("Application key already exists."));
        var response = await Controller(service).Create(new(TenantA, "invoice", "Invoice", 0, "{}"), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(response.Result);
    }

    [Fact]
    public async Task CreateDraft_ForeignTenantReference_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.CreateAsync(TenantA, It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationForeignTenantException("Foreign tenant reference."));
        var response = await Controller(service).Create(new(TenantA, "invoice", "Invoice", 0, "{}"), CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
    }

    [Fact]
    public async Task CreateDraft_ValidationFailure_Returns400()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.CreateAsync(TenantA, It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationValidationException("Malformed application metadata."));
        var response = await Controller(service).Create(new(TenantA, "invoice", "Invoice", 0, "{}"), CancellationToken.None);
        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.IsType<ProblemDetails>(result.Value);
    }

    [Fact]
    public async Task CreateDraft_CrossTenantRequest_Returns403WithoutServiceCall()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).Create(new(TenantB, "invoice", "Invoice", 0, "{}"), CancellationToken.None);
        Assert.IsType<ForbidResult>(response.Result);
        service.Verify(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- Update draft (expectedVersion concurrency)
    [Fact]
    public async Task UpdateDraft_MatchingExpectedVersion_ReturnsNoContent()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var response = await Controller(service).Update("app-1", new(TenantA, "invoice", "Invoice", 1, "{}"), CancellationToken.None);
        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task UpdateDraft_StaleExpectedVersion_Returns409WithoutMutation()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationDefinitionConflictException("The application revision has changed."));
        var response = await Controller(service).Update("app-1", new(TenantA, "invoice", "Invoice", 99, "{}"), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(response);
        // No data loss: the service rejected the stale write and the API surfaced 409, not a mutation success.
        service.Verify(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDraft_ForeignTenantReference_Returns403()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationForeignTenantException("Foreign tenant reference."));
        var response = await Controller(service).Update("app-1", new(TenantA, "invoice", "Invoice", 1, "{}"), CancellationToken.None);
        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task UpdateDraft_ValidationFailure_Returns400()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationValidationException("Malformed application metadata."));
        var response = await Controller(service).Update("app-1", new(TenantA, "invoice", "Invoice", 1, "{}"), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task UpdateDraft_Missing_ReturnsNotFound()
    {
        var service = new Mock<IApplicationDefinitionService>();
        service.Setup(s => s.UpdateAsync(TenantA, "app-1", It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var response = await Controller(service).Update("app-1", new(TenantA, "invoice", "Invoice", 1, "{}"), CancellationToken.None);
        Assert.IsType<NotFoundResult>(response);
    }

    [Fact]
    public async Task UpdateDraft_CrossTenantRequest_Returns403WithoutServiceCall()
    {
        var service = new Mock<IApplicationDefinitionService>();
        var response = await Controller(service).Update("app-1", new(TenantB, "invoice", "Invoice", 1, "{}"), CancellationToken.None);
        Assert.IsType<ForbidResult>(response);
        service.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApplicationDefinitionWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
