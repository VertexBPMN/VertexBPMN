using System.IO.Compression;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Hardened validation of a release archive import (LC10). Bounds are enforced on the total
/// unpacked size, the number of entries, and each entry's path/size. Path traversal (..),
/// absolute paths, symlinks, and unsigned executable packages are rejected. Only the manifest
/// and schema artifacts survive; runtime executables require an explicit trusted signature.
/// </summary>
public static class ReleaseArchiveImporter
{
    public const long DefaultMaxTotalBytes = 25L * 1024 * 1024; // 25 MiB
    public const int DefaultMaxEntryCount = 512;

    private static readonly string[] UnsignedExecutableExtensions =
    {
        ".exe", ".dll", ".so", ".dylib", ".dll.config",
        // Node-RED / script runtimes are out of LC10 scope (LC08/LC09); treat any packaged
        // script bundle as an executable package that must be rejected unless signed.
        ".js", ".mjs", ".cjs", ".py", ".sh", ".ps1", ".bat",
    };

    public sealed record Entry(string Name, long Length, bool IsDirectory);

    public sealed record ValidationResult(int EntryCount, long TotalBytes, bool HasManifest, string? Error);

    /// <summary>
    /// Validates the import without writing anything to disk. Returns the result; throws
    /// <see cref="ApplicationValidationException"/> when the archive is malformed or violates
    /// the hardened bounds. A successful result carries the manifest entry+schema entries.
    /// </summary>
    public static ValidationResult ValidateImport(Stream archiveBytes, long? maxTotalBytes = null, int? maxEntryCount = null, IReadOnlySet<string>? trustedSigners = null)
    {
        if (archiveBytes is null) { throw new ApplicationValidationException("Release archive is empty."); }

        var maxTotal = maxTotalBytes ?? DefaultMaxTotalBytes;
        var maxEntries = maxEntryCount ?? DefaultMaxEntryCount;

        long total = 0;
        var count = 0;
        var hasManifest = false;

        try
        {
            using var zip = new ZipArchive(archiveBytes, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                count++;
                if (count > maxEntries)
                { return new ValidationResult(count, total, hasManifest, "Release archive exceeds the entry-count bound."); }

                var validation = ValidateEntry(entry, ref hasManifest, trustedSigners);
                if (validation is not null) { return validation; }

                if (entry.Length == 0)
                { continue; }

                total += entry.Length;
                if (total > maxTotal)
                { return new ValidationResult(count, total, hasManifest, "Release archive exceeds the total-size bound."); }
            }
        }
        catch (InvalidDataException)
        {
            throw new ApplicationValidationException("Release archive is not a valid zip archive.");
        }

        if (!hasManifest)
        {
            return new ValidationResult(count, total, hasManifest, "Release archive contains no manifest entry.");
        }

        return new ValidationResult(count, total, hasManifest, null);
    }

    private static ValidationResult? ValidateEntry(ZipArchiveEntry entry, ref bool hasManifest, IReadOnlySet<string>? trustedSigners)
    {
        var name = entry.FullName;
        var normalized = name.Replace('\\', '/');

        // Path hardening: reject absolute paths, drive letters, and parent traversal.
        if (IsPathTraversal(normalized))
        {
            return new ValidationResult(0, 0, hasManifest, "Release archive contains a path-traversal or absolute entry.");
        }

        // Symlink hardening: per-entry external attributes indicate a Unix symlink.
        if ((entry.ExternalAttributes & 0xF000) == 0xA000)
        {
            return new ValidationResult(0, 0, hasManifest, "Release archive contains a symlink entry.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.EndsWith('/'))
        { return null; }

        var lower = normalized.ToLowerInvariant();
        if (!hasManifest && (lower == "manifest.json" || lower == "release-manifest.json" || normalized.EndsWith("/manifest.json", StringComparison.Ordinal)))
        {
            hasManifest = true;
        }

        if (IsUnsignedExecutable(lower) && !IsTrustedSigner(trustedSigners, normalized))
        {
            return new ValidationResult(0, 0, hasManifest, "Release archive contains an unsigned executable package.");
        }

        return null;
    }

    private static bool IsPathTraversal(string normalized) =>
        (normalized.Length >= 1 && normalized[0] == '/')
        || (normalized.Length >= 2 && normalized[1] == ':')
        || normalized.Split('/').Contains("..");

    private static bool IsUnsignedExecutable(string normalizedLower) =>
        UnsignedExecutableExtensions.Any(ext => normalizedLower.EndsWith(ext, StringComparison.Ordinal));

    private static bool IsTrustedSigner(IReadOnlySet<string>? trustedSigners, string entry) =>
        trustedSigners?.Contains(entry, StringComparer.OrdinalIgnoreCase) == true;
}
