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

        Assert.Throws<IOException>(() => StudioModelUpload.OpenReadStream(file.Object));

        file.Verify(value => value.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
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
