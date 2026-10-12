using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	private const int MaxProfileBytes = 32 * 1024;

	public async Task CreateConnectionProfileAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, RepositoryConnectionProfile profile,
		CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		DemandProfileTenant(context, profile);
		if (profile.Revision != 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		var json = SerializeProfile(profile);
		var row = new SourceControlConnectionProfileRecord
		{
			Id = profile.Id,
			TenantId = context.TenantId,
			Revision = 1,
			ProfileJson = json
		};
		if (await db.SourceControlConnectionProfiles.AnyAsync(x => x.TenantId == context.TenantId
			&& x.Id == profile.Id, cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}

		db.SourceControlConnectionProfiles.Add(row);
		try
		{
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			db.Entry(row).State = EntityState.Detached;
			if (await db.SourceControlConnectionProfiles.AnyAsync(x => x.TenantId == context.TenantId
				&& x.Id == profile.Id, cancellationToken))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}

			throw;
		}
	}

	public async Task<RepositoryConnectionProfile?> FindConnectionProfileAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, Guid profileId, CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		var row = await db.SourceControlConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(
			x => x.TenantId == context.TenantId && x.Id == profileId && !x.IsRevoked, cancellationToken);
		return row is null ? null : DecodeProfile(row);
	}

	public async Task<bool> ReplaceConnectionProfileAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, long expectedRevision,
		RepositoryConnectionProfile profile, CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		DemandProfileTenant(context, profile);
		ValidateProfileRevision(expectedRevision);
		if (profile.Revision != expectedRevision + 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		var json = SerializeProfile(profile);
		return await db.SourceControlConnectionProfiles.Where(x => x.TenantId == context.TenantId
			&& x.Id == profile.Id && x.Revision == expectedRevision && !x.IsRevoked)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.ProfileJson, json)
				.SetProperty(x => x.Revision, profile.Revision), cancellationToken) == 1;
	}

	public async Task<bool> RevokeConnectionProfileAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, Guid profileId, long expectedRevision,
		CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		ValidateProfileRevision(expectedRevision);
		var row = await db.SourceControlConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(
			x => x.TenantId == context.TenantId && x.Id == profileId && x.Revision == expectedRevision
				&& !x.IsRevoked, cancellationToken);
		if (row is null)
		{
			return false;
		}

		var next = DecodeProfile(row) with { Revision = expectedRevision + 1 };
		var json = SerializeProfile(next);
		return await db.SourceControlConnectionProfiles.Where(x => x.TenantId == context.TenantId
			&& x.Id == profileId && x.Revision == expectedRevision && !x.IsRevoked)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRevoked, true)
				.SetProperty(x => x.ProfileJson, json).SetProperty(x => x.Revision, next.Revision),
				cancellationToken) == 1;
	}

	private void DemandProfileAdministration(IReadOnlyCollection<string> authenticatedRoles)
	{
		Relational();
		if (!authenticatedRoles.Contains("Admin", StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
		}
	}

	private static void DemandProfileTenant(SourceControlContext context, RepositoryConnectionProfile profile)
	{
		if (profile.TenantId != context.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
		}
	}

	private static void ValidateProfileRevision(long revision)
	{
		if (revision <= 0 || revision == long.MaxValue)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}

	private static string SerializeProfile(RepositoryConnectionProfile profile)
	{
		RepositoryConnectionProfilePolicy.Validate(profile);
		var json = JsonSerializer.Serialize(profile);
		if (Encoding.UTF8.GetByteCount(json) > MaxProfileBytes)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}

		return json;
	}

	private static RepositoryConnectionProfile DecodeProfile(SourceControlConnectionProfileRecord row)
	{
		try
		{
			if (Encoding.UTF8.GetByteCount(row.ProfileJson) > MaxProfileBytes)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}

			var profile = JsonSerializer.Deserialize<RepositoryConnectionProfile>(row.ProfileJson)
				?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			RepositoryConnectionProfilePolicy.Validate(profile);
			if (profile.Id != row.Id || profile.TenantId != row.TenantId || profile.Revision != row.Revision)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}

			return profile;
		}
		catch (Exception error) when (error is JsonException or ArgumentException or SourceControlSecurityException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}
}
