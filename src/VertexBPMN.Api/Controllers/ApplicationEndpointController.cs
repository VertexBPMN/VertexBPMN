using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Api.Services;
using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/apps/{appKey}/endpoints/{endpointKey}")]
[Authorize(Policy = "ReadOnly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "MVC discovery requires a public controller.")]
public sealed class ApplicationEndpointController(BpmnDbContext db, IApplicationDefinitionService applications,
    IPersonalTaskService tasks, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("{**path}")]
    public async Task<IActionResult> Get(string appKey, string endpointKey, string? path, CancellationToken ct)
    {
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) { return Forbid(); }
        var id = await db.ApplicationDefinitions.AsNoTracking().Where(x => x.TenantId == tenant && x.Key == appKey)
            .Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (id is null) { return NotFound(); }
        var activeRevision = await db.ApplicationReleases.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.ApplicationDefinitionId == id && x.State == ApplicationReleaseState.Active)
            .Select(x => x.ApplicationRevisionId).SingleOrDefaultAsync(ct);
        var revision = activeRevision is null ? await applications.GetReleasedRevisionAsync(tenant, id, ct)
            : await db.ApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activeRevision
                && x.TenantId == tenant && x.ApplicationDefinitionId == id, ct);
        if (revision is null) { return NotFound(); }
        ApplicationComponentCatalog.Validate(revision.ContentJson);
        var endpoint = ApplicationEndpointMetadata.Read(revision.ContentJson).SingleOrDefault(x => x.Key == endpointKey);
        if (endpoint is null || endpoint.Method != "GET" || endpoint.Action.Type != "data-source-read"
            || ReleasedDataSourceReader.ResolveSanctionedRead(revision.ContentJson, endpoint.Action.Target) is null)
        { return NotFound(); }
        if (endpoint.Permission is not ("ReadOnly" or "ProcessManager" or "AdminOnly")
            || !(await authorization.AuthorizeAsync(User, null, endpoint.Permission)).Succeeded) { return Forbid(); }
        using var content = System.Text.Json.JsonDocument.Parse(revision.ContentJson);
        var source = content.RootElement.GetProperty("dataSources").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == endpoint.Action.Target);
        if (!source.TryGetProperty("query", out var query) || query.GetString() != "personal-task-inbox"
            || !source.TryGetProperty("bindingVersion", out var version) || version.GetInt32() != 2)
        { return NotFound(); }
        var parameters = EndpointPathParameters.Read(endpoint, path);
        if (parameters is null || endpoint.PathParameters.Count != 1 || endpoint.PathParameters[0] != "search")
        { return NotFound(); }
        if (!EndpointJsonSchema.Matches(endpoint.InputSchema, parameters.Value))
        { return BadRequest(new { code = "endpoint_input_invalid" }); }
        var result = await tasks.QueryInboxAsync(new TaskInboxQuery(tenant,
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "",
            User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(),
            Search: parameters.Value.GetProperty("search").GetString(), PageSize: 20), ct);
        var response = System.Text.Json.JsonSerializer.SerializeToElement(new
            { items = result.Items.Select(x => new { id = x.Id.ToString(), name = x.Name }).ToArray() });
        if (System.Text.Encoding.UTF8.GetByteCount(response.GetRawText()) > endpoint.Limits.MaxResponseBytes)
        { return StatusCode(StatusCodes.Status502BadGateway, new { code = "endpoint_response_too_large" }); }
        return EndpointJsonSchema.Matches(endpoint.OutputSchema, response)
            ? Ok(response) : StatusCode(StatusCodes.Status502BadGateway, new { code = "endpoint_output_invalid" });
    }
}
