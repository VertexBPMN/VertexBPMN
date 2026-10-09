using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics.CodeAnalysis;

namespace VertexBPMN.Studio.Services;

internal static class StudioModelUpload
{
	public const long MaximumBytes = 10 * 1024 * 1024;
	[SuppressMessage("Security", "S5693", Justification = "Model imports intentionally support 10 MiB. Both the declared size and the browser stream are bounded by this fixed limit; no caller can request an unlimited stream.")]
	public static Stream OpenReadStream(IBrowserFile file, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(file);
		if (file.Size > MaximumBytes)
		{
			throw new IOException("Model uploads must not exceed 10 MB.");
		}

		return file.OpenReadStream(MaximumBytes, cancellationToken);
	}
}
