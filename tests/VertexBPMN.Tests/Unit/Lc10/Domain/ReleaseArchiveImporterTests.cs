using System.IO.Compression;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using Xunit;

#pragma warning disable RCS1266 // JSON test fixtures require escaped quotes.

namespace VertexBPMN.Tests.Unit.Lc10.Domain;

public sealed class ReleaseArchiveImporterTests
{
    private static byte[] BuildArchive(Action<ZipArchive> populate)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            populate(zip);
        }
        return stream.ToArray();
    }

    [Fact]
    public void ValidArchiveWithManifest_IsAccepted()
    {
        var bytes = BuildArchive(zip =>
        {
            var entry = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"k\",\"revision\":1,\"hash\":\"abc\"}");
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes));

        Assert.Null(result.Error);
        Assert.True(result.HasManifest);
    }

    [Fact]
    public void ArchiveWithoutManifest_IsRejected()
    {
        var bytes = BuildArchive(zip => zip.CreateEntry("process.bpmn"));

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes));

        Assert.NotNull(result.Error);
        Assert.Contains("no manifest", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PathTraversalEntry_IsRejected()
    {
        var bytes = BuildArchive(zip =>
        {
            zip.CreateEntry("../evil.txt");
            var manifest = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"schemaVersion\":\"1\"}");
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes));

        Assert.NotNull(result.Error);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbsolutePathEntry_IsRejected()
    {
        var bytes = BuildArchive(zip =>
        {
            zip.CreateEntry("/etc/passwd");
            var manifest = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"schemaVersion\":\"1\"}");
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes));

        Assert.NotNull(result.Error);
        Assert.Contains("absolute", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsignedExecutablePackage_IsRejected()
    {
        var bytes = BuildArchive(zip =>
        {
            zip.CreateEntry("runtime.js");
            var manifest = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"schemaVersion\":\"1\"}");
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes));

        Assert.NotNull(result.Error);
        Assert.Contains("unsigned executable", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SignedExecutablePackage_IsAcceptedWhenTrusted()
    {
        var bytes = BuildArchive(zip =>
        {
            zip.CreateEntry("runtime.js");
            var manifest = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"schemaVersion\":\"1\"}");
        });

        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "runtime.js" };
        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes), trustedSigners: trusted);

        Assert.Null(result.Error);
    }

    [Fact]
    public void EntryCountOverBound_IsRejected()
    {
        var bytes = BuildArchive(zip =>
        {
            for (var i = 0; i < 600; i++)
            {
                zip.CreateEntry($"file{i}.txt");
            }
            var manifest = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"schemaVersion\":\"1\"}");
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes), maxEntryCount: 512);

        Assert.NotNull(result.Error);
        Assert.Contains("entry-count", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TotalSizeOverBound_IsRejected()
    {
        var payload = new string('x', 4096);
        var bytes = BuildArchive(zip =>
        {
            var blob = zip.CreateEntry("blob.txt");
            using var blobWriter = new StreamWriter(blob.Open());
            blobWriter.Write(payload);
            blobWriter.Dispose(); // close the entry before creating the next one

            var manifest = zip.CreateEntry("manifest.json");
            using var manifestWriter = new StreamWriter(manifest.Open());
            manifestWriter.Write("{\"schemaVersion\":\"1\"}");
            manifestWriter.Dispose();
        });

        var result = ReleaseArchiveImporter.ValidateImport(new MemoryStream(bytes), maxTotalBytes: 128);

        Assert.NotNull(result.Error);
        Assert.Contains("total-size", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedZip_ThrowsValidationException()
    {
        var junk = new byte[] { 1, 2, 3, 4, 5 };
        Assert.Throws<ApplicationValidationException>(() => ReleaseArchiveImporter.ValidateImport(new MemoryStream(junk)));
    }
}
