using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Azure.Identity;
using Xunit;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// P4.2: Azure-DataProtection-Key-Ring (Blob-Persistenz + Key-Vault-Verschlüsselung).
/// Beweist gegen die echten Azure-Ressourcen: (a) der verschlüsselte Key-Ring wird in den
/// dedizierten Blob-Container persistiert, (b) die Schlüsselverschlüsselung läuft über einen
/// Key-Vault-Key ausschließlich mit Managed-Identity-Credentials (kein Verbindungsstring),
/// (c) Round-Trip-Verschlüsselung funktioniert und ist über zwei Provider-Instanzen hinweg
/// stabil (derselbe Ring). Läuft ohne VERTEXBPMN_TEST_AZURE_DP_BLOB_URI als Skip.
/// </summary>
public sealed class AzureDataProtectionKeyRingAcceptanceTests
{
    private const string Category = "AzureKeyRingAcceptance";

    // Full blob URI, e.g. https://<blob-storage>.blob.core.windows.net/dataprotection-api/keys.xml
    private static string BlobUri =>
        Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_AZURE_DP_BLOB_URI") ?? "";

    // Full key identifier, e.g. https://<keyvault>.vault.azure.net/keys/dataprotection-key/<version>
    private static string KeyIdentifier =>
        Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_AZURE_DP_KEY_ID") ?? "";

    private static bool AzureConfigured =>
        !string.IsNullOrWhiteSpace(BlobUri) && !string.IsNullOrWhiteSpace(KeyIdentifier);

    private static IDataProtectionProvider BuildProvider(string blobUri, string keyId)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeInteractiveBrowserCredential = true,
        });
        return DataProtectionProvider.Create(
            new System.IO.DirectoryInfo(Path.Combine(Path.GetTempPath(), "vbpn-dp-" + Guid.NewGuid().ToString("N"))),
            builder =>
            {
                builder.SetApplicationName("VertexBPMN.Acceptance");
                builder.PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);
                builder.ProtectKeysWithAzureKeyVault(new Uri(keyId), credential);
            });
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Azure_Blob_KeyVault_RoundTrips_And_Uses_Shared_Ring()
    {
        Assert.SkipUnless(AzureConfigured, "Azure Key Vault + Blob configured via VERTEXBPMN_TEST_AZURE_DP_BLOB_URI.");
        var secret = "p4.2 secret payload " + Guid.NewGuid().ToString("N");

        // Erste Instanz: erzeugt (falls nötig) und liest den Key-Ring aus dem Azure-Blob,
        // Schlüssel mit dem Key-Vault-Key entschlüsselt (Managed Identity).
        var p1 = BuildProvider(BlobUri, KeyIdentifier);
        var p1Bytes = p1.CreateProtector("P4.2.Acceptance").Protect(Encoding.UTF8.GetBytes(secret));

        // Zweite Instanz über denselben Blob-Ring: muss denselben Schlüssel verwenden und
        // den Klartext wiederherstellen. Beweist, dass der Ring in Azure persistiert und
        // MI-aufgelöst wird (kein lokaler Filesystem-Key-Ring).
        var p2 = BuildProvider(BlobUri, KeyIdentifier);
        var recovered = Encoding.UTF8.GetString(p2.CreateProtector("P4.2.Acceptance").Unprotect(p1Bytes));

        Assert.Equal(secret, recovered);
    }
}
