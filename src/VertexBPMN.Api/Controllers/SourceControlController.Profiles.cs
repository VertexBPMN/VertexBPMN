using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Api.Controllers;

public sealed partial class SourceControlController
{
	[HttpGet("repositories/{id:guid}/availability")]
	public Task<ActionResult<SourceControlAvailability>> RepositoryAvailability(Guid id, CancellationToken cancellationToken) =>
		RunAsync(async () =>
		{
			var global = await provider.GetAvailabilityAsync(cancellationToken);
			return global.Available
				? await store.GetRepositoryAvailabilityAsync(Context(), id, await RolesAsync(cancellationToken), profileOptions, cancellationToken)
				: global;
		});

	[HttpPost("connection-profiles")]
	public Task<ActionResult<RepositoryConnectionProfile>> CreateProfile(RepositoryConnectionProfile profile, CancellationToken cancellationToken) =>
		RunAsync(async () =>
		{
			await store.CreateConnectionProfileAsync(Context(), await RolesAsync(cancellationToken), profile, cancellationToken);
			return profile;
		});

	[HttpGet("connection-profiles/{id:guid}")]
	public Task<ActionResult<RepositoryConnectionProfile>> Profile(Guid id, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.FindConnectionProfileAsync(Context(), await RolesAsync(cancellationToken), id, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound));

	[HttpPut("connection-profiles/{id:guid}")]
	public Task<ActionResult<bool>> ReplaceProfile(Guid id, long expectedRevision, RepositoryConnectionProfile profile, CancellationToken cancellationToken) =>
		RunAsync(async () =>
		{
			if (profile.Id != id)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			return await store.ReplaceConnectionProfileAsync(Context(), await RolesAsync(cancellationToken), expectedRevision, profile, cancellationToken);
		});

	[HttpDelete("connection-profiles/{id:guid}")]
	public Task<ActionResult<bool>> RevokeProfile(Guid id, long expectedRevision, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.RevokeConnectionProfileAsync(Context(), await RolesAsync(cancellationToken), id, expectedRevision, cancellationToken));

	[HttpPut("repositories/{id:guid}/connection-profile")]
	public Task<ActionResult<bool>> AssignProfile(Guid id, ProfileAssignment request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.AssignConnectionProfileAsync(Context(), await RolesAsync(cancellationToken), id,
			request.BindingRevision, request.ProfileId, request.ProfileRevision, cancellationToken));

	[HttpGet("operations/{id:guid}/read-result")]
	public Task<ActionResult<SourceControlReadResult>> ReadResult(Guid id, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.GetProfiledReadResultAsync(Context(), id, await RolesAsync(cancellationToken),
			profileOptions, cancellationToken));

	[HttpPost("repositories/{id:guid}/read-requests")]
	public async Task<ActionResult<Guid>> SubmitRead(Guid id, ReadRequest request, CancellationToken cancellationToken)
	{
		var result = await RunAsync(async () =>
		{
			if (request.SchemaVersion != 1)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			return await store.EnqueueProfiledReadAsync(Context(), id, await RolesAsync(cancellationToken), new(request.Key),
				new(request.Kind, request.Commit is null ? null : new(request.Commit), request.Path, request.PageSize ?? 0,
					request.Cursor, request.Generation ?? Guid.Empty, request.Snapshot?.ToSnapshot()), profileOptions, cancellationToken);
		});
		return result.Result is null ? Accepted($"/api/source-control/operations/{result.Value}/read-result", result.Value) : result;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public HTTP action contract required for MVC binding and OpenAPI.")]
	public sealed record ReadRequest([property: System.Text.Json.Serialization.JsonRequired] GitReadQueryKind Kind,
		[property: System.Text.Json.Serialization.JsonRequired] string Key, string? Commit, string? Path,
		int? PageSize, string? Cursor, Guid? Generation, SnapshotResponse? Snapshot)
	{
		[System.Text.Json.Serialization.JsonRequired]
		public int SchemaVersion { get; init; } = 1;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public HTTP action contract required for MVC binding and OpenAPI.")]
	public sealed record ProfileAssignment([property: System.Text.Json.Serialization.JsonRequired] long BindingRevision,
		[property: System.Text.Json.Serialization.JsonRequired] Guid ProfileId,
		[property: System.Text.Json.Serialization.JsonRequired] long ProfileRevision);
}
