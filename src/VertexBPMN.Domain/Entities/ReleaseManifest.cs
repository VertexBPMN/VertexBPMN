using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// LC10 release manifest: schema-version, artifact-kind, key, revision, hash, dependencies,
/// target profile, credential alias. Parsed from the archive entry and validated before a
/// release can be prepared/activated. Unknown or missing mandatory fields are rejected so a
/// malformed or foreign manifest can never reach activation.
/// </summary>
public sealed record ReleaseManifest
{
    public const int MaxSchemaVersion = 2;
    public const string ArtifactKindApplication = "application";

    public string SchemaVersion { get; init; } = "1";
    public string ArtifactKind { get; init; } = ArtifactKindApplication;
    public string Key { get; init; } = string.Empty;
    public int Revision { get; init; }
    public string Hash { get; init; } = string.Empty;
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public string TargetProfile { get; init; } = "default";
    public string CredentialAlias { get; init; } = string.Empty;

    public static ReleaseManifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) { throw new ApplicationValidationException("Release manifest is empty."); }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var schemaVersion = root.TryGetProperty("schemaVersion", out var sv) ? sv.GetString() ?? "1" : "1";
        if (!int.TryParse(schemaVersion, out var schemaNumber) || schemaNumber > MaxSchemaVersion)
        { throw new ApplicationValidationException("Release manifest schema version is unsupported."); }

        var kind = root.TryGetProperty("artifactKind", out var ak) ? ak.GetString() ?? ArtifactKindApplication : ArtifactKindApplication;
        var key = root.TryGetProperty("key", out var k) ? k.GetString() ?? string.Empty : string.Empty;
        var revision = root.TryGetProperty("revision", out var r) ? r.GetInt32() : 0;
        var hash = root.TryGetProperty("hash", out var h) ? h.GetString() ?? string.Empty : string.Empty;
        var targetProfile = root.TryGetProperty("targetProfile", out var tp) ? tp.GetString() ?? "default" : "default";
        var credentialAlias = root.TryGetProperty("credentialAlias", out var ca) ? ca.GetString() ?? string.Empty : string.Empty;

        var dependencies = new List<string>();
        if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            foreach (var dep in deps.EnumerateArray())
            {
                if (dep.ValueKind == JsonValueKind.String)
                {
                    dependencies.Add(dep.GetString()!);
                }
                else if (dep.ValueKind == JsonValueKind.Object
                         && dep.TryGetProperty("key", out var dk) && !string.IsNullOrWhiteSpace(dk.GetString()))
                {
                    dependencies.Add(dk.GetString()!);
                }
            }
        }

        return new ReleaseManifest
        {
            SchemaVersion = schemaVersion,
            ArtifactKind = kind,
            Key = key,
            Revision = revision,
            Hash = hash,
            Dependencies = dependencies,
            TargetProfile = targetProfile,
            CredentialAlias = credentialAlias,
        };
    }

    public void ValidateForPrepare(string expectedKey, int expectedRevision)
    {
        if (ArtifactKind != ArtifactKindApplication)
        { throw new ApplicationValidationException("Release manifest artifact-kind must be 'application'."); }
        if (string.IsNullOrWhiteSpace(Key))
        { throw new ApplicationValidationException("Release manifest key is required."); }
        if (Key != expectedKey)
        { throw new ApplicationForeignTenantException("Release manifest key does not match the application definition."); }
        if (Revision <= 0 || Revision != expectedRevision)
        { throw new ApplicationValidationException("Release manifest revision does not match the pinned application revision."); }
        if (string.IsNullOrWhiteSpace(Hash))
        { throw new ApplicationValidationException("Release manifest hash is required."); }
        if (Dependencies.Any(string.IsNullOrWhiteSpace))
        { throw new ApplicationValidationException("Release manifest contains an empty dependency."); }
    }

    public static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    public static string Serialize(ReleaseManifest manifest) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = manifest.SchemaVersion,
            artifactKind = manifest.ArtifactKind,
            key = manifest.Key,
            revision = manifest.Revision,
            hash = manifest.Hash,
            dependencies = manifest.Dependencies,
            targetProfile = manifest.TargetProfile,
            credentialAlias = manifest.CredentialAlias,
        });
}
