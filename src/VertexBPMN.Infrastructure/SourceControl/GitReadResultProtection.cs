using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Versioned result contracts bound to the successful operation and its fence, never caller-selected CLR types.</summary>
internal sealed class GitReadResultProtection(IDataProtectionProvider protection)
{
	private const int MaximumJsonBytes = 8 * 1024 * 1024;
	private const int MaximumProtectedCharacters = 12 * 1024 * 1024;

	internal string Protect<T>(SourceControlOperationRecord row, T result)
	{
		_ = Contract<T>();
		if (result is null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var bytes = result is ModelSnapshot snapshot
			? JsonSerializer.SerializeToUtf8Bytes(new StoredReadSnapshot(snapshot.Path, snapshot.Kind,
				snapshot.DocumentGeneration, snapshot.LocalRevision, snapshot.ContentSha256, snapshot.CopyContent()))
			: JsonSerializer.SerializeToUtf8Bytes(result);
		if (bytes.Length > MaximumJsonBytes)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		return Convert.ToBase64String(Protector<T>(row).Protect(bytes));
	}

	internal T Unprotect<T>(SourceControlOperationRecord row)
	{
		if (row.State != (int)SourceControlOperationState.Succeeded || string.IsNullOrEmpty(row.ProtectedResult)
			|| row.ProtectedResult.Length > MaximumProtectedCharacters)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		try
		{
			var bytes = Protector<T>(row).Unprotect(Convert.FromBase64String(row.ProtectedResult));
			if (bytes.Length > MaximumJsonBytes)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (typeof(T) == typeof(ModelSnapshot))
			{
				var saved = JsonSerializer.Deserialize<StoredReadSnapshot>(bytes)
					?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
				var snapshot = new ModelSnapshot(saved.Path, saved.Kind, saved.Generation, saved.Revision, saved.Bytes);
				if (snapshot.ContentSha256 != saved.ContentSha256)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
				}
				return (T)(object)snapshot;
			}
			return JsonSerializer.Deserialize<T>(bytes) ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		catch (Exception error) when (error is JsonException or CryptographicException or FormatException or ArgumentException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private IDataProtector Protector<T>(SourceControlOperationRecord row) => protection.CreateProtector(
		"VertexBPMN.SourceControl.ReadResult.v1", Contract<T>(), row.TenantId, row.RepositoryId.ToString("N"), row.ActorId,
		row.Id.ToString("N"), row.Fence.ToString(CultureInfo.InvariantCulture), row.RequestHash);

	private static string Contract<T>() => typeof(T) switch
	{
		var type when type == typeof(string) => "revision-text",
		var type when type == typeof(RevisionSelection) => "branch-revision",
		var type when type == typeof(SourceControlPage<string>) => "branch-page",
		var type when type == typeof(SourceControlPage<RepositoryFile>) => "model-page",
		var type when type == typeof(ModelSnapshot) => "model-snapshot",
		var type when type == typeof(SourceControlPage<CommitSummary>) => "history-page",
		var type when type == typeof(ModelDiff) => "model-diff",
		_ => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput)
	};
}
