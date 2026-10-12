using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class HostingExecutionProtection(IDataProtectionProvider protection)
{
	internal string Protect(SourceControlOperationRecord row, AcceptedHostingExecution execution)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(execution);
		if (bytes.Length > 512 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		return Convert.ToBase64String(Protector(row).Protect(bytes));
	}

	internal AcceptedHostingExecution Unprotect(SourceControlOperationRecord row)
	{
		if (row.ExecutionContractVersion != AcceptedHostingExecution.ContractVersion
			|| string.IsNullOrEmpty(row.ProtectedExecutionTarget) || row.ProtectedExecutionTarget.Length > 768 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		try
		{
			var bytes = Protector(row).Unprotect(Convert.FromBase64String(row.ProtectedExecutionTarget));
			if (bytes.Length > 512 * 1024)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var execution = JsonSerializer.Deserialize<AcceptedHostingExecution>(bytes);
			if (execution is null || execution.SchemaVersion != AcceptedHostingExecution.ContractVersion
				|| execution.Target is null || execution.HostingTarget is null || execution.ApiPolicy?.Network is null)
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
		"VertexBPMN.SourceControl.HostingExecution.v3", row.TenantId, row.RepositoryId.ToString("N"), row.ActorId,
		row.Id.ToString("N"), row.Kind.ToString(System.Globalization.CultureInfo.InvariantCulture), row.IdempotencyKey, row.RequestHash);
}
