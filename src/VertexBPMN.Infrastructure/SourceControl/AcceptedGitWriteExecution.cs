using System.Text.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Transport-specific write contracts retain their original serialized identities.</summary>
internal sealed record AcceptedGitWriteExecution(AcceptedGitHttpsExecution? Https, AcceptedGitSshWriteExecution? Ssh)
{
	internal SourceControlExecutionTarget Target => Https?.Target ?? Ssh!.Target;
	internal int ContractVersion => Https is not null ? AcceptedGitHttpsExecution.ContractVersion : AcceptedGitSshWriteExecution.ContractVersion;
	internal string? WriteRequestDigest => Https?.WriteRequestDigest ?? Ssh?.WriteRequestDigest;
	internal byte[] FingerprintBytes() => Https is not null
		? JsonSerializer.SerializeToUtf8Bytes(Https with { WriteRequestDigest = null })
		: JsonSerializer.SerializeToUtf8Bytes(Ssh! with { WriteRequestDigest = null });
	internal AcceptedGitWriteExecution WithDigest(string digest) => Https is not null
		? new(Https with { WriteRequestDigest = digest }, null)
		: new(null, Ssh! with { WriteRequestDigest = digest });
}
