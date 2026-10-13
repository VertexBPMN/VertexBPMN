using System.Text.Json.Serialization;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record StoredReadSnapshot([property: JsonRequired] string Path, [property: JsonRequired] SourceModelKind Kind,
	[property: JsonRequired] Guid Generation, [property: JsonRequired] long Revision,
	[property: JsonRequired] string ContentSha256, [property: JsonRequired] byte[] Bytes)
{
	internal ModelSnapshot Restore()
	{
		var snapshot = new ModelSnapshot(Path, Kind, Generation, Revision, Bytes);
		if (snapshot.ContentSha256 != ContentSha256)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		return snapshot;
	}
}
