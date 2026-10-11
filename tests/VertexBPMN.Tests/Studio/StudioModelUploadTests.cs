using Microsoft.AspNetCore.Components.Forms;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Studio;

public sealed class StudioModelUploadTests
{
	[Fact]
	public void OversizedFile_IsRejectedBeforeOpeningTheStream()
	{
		var file = new Mock<IBrowserFile>(MockBehavior.Strict);
		file.SetupGet(value => value.Size).Returns(StudioModelUpload.MaximumBytes + 1);

		Assert.Throws<IOException>(() => StudioModelUpload.OpenReadStream(file.Object, cancellationToken: TestContext.Current.CancellationToken));

		file.Verify(value => value.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The strict mock returns the already using-owned MemoryStream, and Assert.Same verifies that alias; its lifetime is bounded by this test.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S5693", Justification = "This strict IBrowserFile mock verifies the production MaximumBytes limit and cancellation token at the exact permitted boundary; it does not accept HTTP request bodies.")]
	public void ExactLimit_IsAcceptedWithBoundedStreamAndCancellation()
	{
		using var stream = new MemoryStream();
		using var cancellation = new CancellationTokenSource();
		var file = new Mock<IBrowserFile>(MockBehavior.Strict);
		file.SetupGet(value => value.Size).Returns(StudioModelUpload.MaximumBytes);
		file.Setup(value => value.OpenReadStream(StudioModelUpload.MaximumBytes, cancellation.Token)).Returns(stream);

		Assert.Same(stream, StudioModelUpload.OpenReadStream(file.Object, cancellation.Token));
		file.VerifyAll();
	}
}
