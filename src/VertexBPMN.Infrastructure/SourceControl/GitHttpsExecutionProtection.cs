using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class GitHttpsExecutionProtection(IDataProtectionProvider protection)
{
	// PEM escaping can expand the bounded 64 KiB CA bundle. Include profile and CIDR metadata.
	private const int MaximumJsonBytes = 512 * 1024;
	private const int MaximumProtectedCharacters = 768 * 1024;

	internal string Protect(SourceControlOperationRecord row, AcceptedGitHttpsExecution execution)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(execution);
		if (bytes.Length > MaximumJsonBytes)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		return Convert.ToBase64String(Protector(row).Protect(bytes));
	}

	internal AcceptedGitHttpsExecution Unprotect(SourceControlOperationRecord row)
	{
		if (row.ExecutionContractVersion != AcceptedGitHttpsExecution.ContractVersion
			|| string.IsNullOrEmpty(row.ProtectedExecutionTarget)
			|| row.ProtectedExecutionTarget.Length > MaximumProtectedCharacters)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		try
		{
			var bytes = Protector(row).Unprotect(Convert.FromBase64String(row.ProtectedExecutionTarget));
			if (bytes.Length > MaximumJsonBytes)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var execution = JsonSerializer.Deserialize<AcceptedGitHttpsExecution>(bytes);
			if (execution is null || execution.SchemaVersion != AcceptedGitHttpsExecution.ContractVersion
				|| execution.Target is null || execution.Policy?.Network is null)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			return execution;
		}
		catch (Exception error) when (error is JsonException or FormatException or CryptographicException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private IDataProtector Protector(SourceControlOperationRecord row) => protection.CreateProtector(
		"VertexBPMN.SourceControl.HttpsExecution.v2", row.TenantId, row.RepositoryId.ToString("N"),
		row.ActorId, row.Id.ToString("N"), row.Kind.ToString(System.Globalization.CultureInfo.InvariantCulture),
		row.IdempotencyKey, row.RequestHash);
}
