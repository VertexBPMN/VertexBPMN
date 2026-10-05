using System.Security.Cryptography;

namespace VertexBPMN.SourceControl.Abstractions;

public enum SourceModelKind { Bpmn, Dmn, Cmmn, Form }

/// <summary>
/// Immutable bytes captured for one document generation/revision. The host still validates
/// the repository path, content policy, size and tenant rights before storing or executing it.
/// </summary>
public sealed class ModelSnapshot
{
    private readonly byte[] _content;

    public string Path { get; }
    public SourceModelKind Kind { get; }
    public Guid DocumentGeneration { get; }
    public long LocalRevision { get; }
    public int ContentLength => _content.Length;
    public string ContentSha256 { get; }

    public ModelSnapshot(string path, SourceModelKind kind, Guid documentGeneration,
        long localRevision, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (documentGeneration == Guid.Empty) throw new ArgumentException("Document generation is required.", nameof(documentGeneration));
        ArgumentOutOfRangeException.ThrowIfNegative(localRevision);
        if (content.IsEmpty) throw new ArgumentException("Model bytes are required.", nameof(content));
        Path = path;
        Kind = kind;
        DocumentGeneration = documentGeneration;
        LocalRevision = localRevision;
        _content = content.ToArray();
        ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(_content));
    }

    public byte[] CopyContent() => (byte[])_content.Clone();

    // Never put model bytes in generated record ToString output or diagnostic logs.
    public override string ToString() => $"ModelSnapshot({Kind}, revision={LocalRevision}, bytes={ContentLength})";
}
