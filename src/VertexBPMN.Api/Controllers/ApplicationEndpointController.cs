using System.Security.Claims;
using System.Text.Json;
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

    [HttpPost]
    public async Task<IActionResult> StartProcess(string appKey, string endpointKey,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        [FromBody] StartProcessIdempotencyRequest? body, CancellationToken ct)
    {
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) { return Forbid(); }
        var id = await db.ApplicationDefinitions.AsNoTracking().Where(x => x.TenantId == tenant && x.Key == appKey)
            .Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (id is null) { return NotFound(); }
        var release = await db.ApplicationReleases.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.ApplicationDefinitionId == id && x.State == ApplicationReleaseState.Active)
            .OrderByDescending(x => x.PointerRevision)
            .Select(x => new { x.Id, x.ApplicationRevisionId, x.BoundProcessDefinitionId }).FirstOrDefaultAsync(ct);
        if (release is null || string.IsNullOrWhiteSpace(release.BoundProcessDefinitionId)) { return NotFound(); }
        var revision = await db.ApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == release.ApplicationRevisionId && x.TenantId == tenant && x.ApplicationDefinitionId == id, ct);
        if (revision is null) { return NotFound(); }
        ApplicationComponentCatalog.Validate(revision.ContentJson);
        var endpoint = ApplicationEndpointMetadata.Read(revision.ContentJson).SingleOrDefault(x => x.Key == endpointKey);
        if (endpoint is null || endpoint.Method != "POST" || endpoint.Action.Type != "start-process"
            || !string.Equals(endpoint.Action.Target, release.BoundProcessDefinitionId, StringComparison.Ordinal))
        { return NotFound(); }
        if (endpoint.Permission is not ("ReadOnly" or "ProcessManager" or "AdminOnly")
            || !(await authorization.AuthorizeAsync(User, null, endpoint.Permission)).Succeeded) { return Forbid(); }
        if (!Guid.TryParse(release.BoundProcessDefinitionId, out _)) { return NotFound(); }

        var idempotencyKey = ResolveIdempotencyKey(idempotencyKeyHeader, body);
        if (idempotencyKey is not null)
        {
            var existing = await FindByKeyAsync(tenant, appKey, endpointKey, idempotencyKey, ct);
            if (existing is not null) { return AcceptedWith(existing, appKey, endpointKey); }
        }
        return await CreateAcceptedAsync(idempotencyKey, tenant, appKey, endpointKey, id,
            revision.Id, release.Id, release.BoundProcessDefinitionId, ct);
    }

    private static string? ResolveIdempotencyKey(string? headerKey, StartProcessIdempotencyRequest? body)
    {
        if (!string.IsNullOrWhiteSpace(headerKey)) { return headerKey.Trim(); }
        var bodyKey = body?.IdempotencyKey;
        return string.IsNullOrWhiteSpace(bodyKey) ? null : bodyKey.Trim();
    }

    private static AcceptedResult AcceptedWith(string operationId, string appKey, string endpointKey)
    {
        var statusUrl = $"/api/apps/{appKey}/endpoints/{endpointKey}/operations/{operationId}";
        return new AcceptedResult(statusUrl, new { operationId, statusUrl, status = "Accepted" });
    }

    private async Task<string?> FindByKeyAsync(string tenant, string appKey, string endpointKey,
        string idempotencyKey, CancellationToken ct) =>
        await db.ApplicationEndpointOperations.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.ApplicationKey == appKey
                && x.EndpointKey == endpointKey && x.IdempotencyKey == idempotencyKey)
            .Select(x => x.OperationId).SingleOrDefaultAsync(ct);

    private async Task<IActionResult> CreateAcceptedAsync(string? idempotencyKey, string tenant, string appKey,
        string endpointKey, string definitionId, string revisionId, string releaseId, string boundDefinitionId,
        CancellationToken ct)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var operation = new ApplicationEndpointOperation
        {
            Id = Guid.NewGuid(), OperationId = operationId, TenantId = tenant,
            ApplicationKey = appKey, EndpointKey = endpointKey, Status = "Accepted",
            IdempotencyKey = idempotencyKey,
            ApplicationDefinitionId = definitionId, ApplicationRevisionId = revisionId,
            ApplicationReleaseId = releaseId, BoundProcessDefinitionId = boundDefinitionId,
            CreatedAtTicks = DateTime.UtcNow.Ticks, UpdatedAtTicks = DateTime.UtcNow.Ticks
        };
        db.ApplicationEndpointOperations.Add(operation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            var concurrent = await FindByKeyAsync(tenant, appKey, endpointKey, idempotencyKey, ct);
            if (concurrent is not null) { return AcceptedWith(concurrent, appKey, endpointKey); }
            throw;
        }
        return AcceptedWith(operationId, appKey, endpointKey);
    }

    [HttpGet("operations/{operationId}")]
    public async Task<IActionResult> GetOperation(string appKey, string endpointKey, string operationId, CancellationToken ct)
    {
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) { return Forbid(); }
        var op = await db.ApplicationEndpointOperations.AsNoTracking().SingleOrDefaultAsync(
            x => x.OperationId == operationId && x.TenantId == tenant
                && x.ApplicationKey == appKey && x.EndpointKey == endpointKey, ct);
        if (op is null) { return NotFound(); }

        // Re-check the CURRENT endpoint policy like POST: an undefined endpoint/release is a
        // 404; an insufficient role for the current policy is a 403.
        var permission = await ResolveCurrentEndpointPermissionAsync(tenant, appKey, endpointKey, ct);
        if (permission is null) { return NotFound(); }
        if (permission is not ("ReadOnly" or "ProcessManager" or "AdminOnly")
            || !(await authorization.AuthorizeAsync(User, null, permission)).Succeeded) { return Forbid(); }

        var result = string.IsNullOrEmpty(op.ResultJson)
            ? null : (object)JsonDocument.Parse(op.ResultJson).RootElement.Clone();
        return Ok(new
        {
            operationId = op.OperationId, appKey = op.ApplicationKey, endpointKey = op.EndpointKey,
            status = op.Status, serviceCode = "start-process", result
        });
    }

    private async Task<string?> ResolveCurrentEndpointPermissionAsync(string tenant, string appKey,
        string endpointKey, CancellationToken ct)
    {
        var definitionId = await db.ApplicationDefinitions.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.Key == appKey).Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (definitionId is null) { return null; }
        var release = await db.ApplicationReleases.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.ApplicationDefinitionId == definitionId
                && x.State == ApplicationReleaseState.Active)
            .OrderByDescending(x => x.PointerRevision)
            .Select(x => new { x.ApplicationRevisionId, x.BoundProcessDefinitionId }).FirstOrDefaultAsync(ct);
        if (release is null || string.IsNullOrWhiteSpace(release.BoundProcessDefinitionId)) { return null; }
        var revision = await db.ApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == release.ApplicationRevisionId && x.TenantId == tenant
                && x.ApplicationDefinitionId == definitionId, ct);
        if (revision is null) { return null; }
        ApplicationComponentCatalog.Validate(revision.ContentJson);
        var endpoint = ApplicationEndpointMetadata.Read(revision.ContentJson).SingleOrDefault(x => x.Key == endpointKey);
        if (endpoint is null || endpoint.Method != "POST" || endpoint.Action.Type != "start-process"
            || !string.Equals(endpoint.Action.Target, release.BoundProcessDefinitionId, StringComparison.Ordinal))
        { return null; }
        return endpoint.Permission;
    }
}
