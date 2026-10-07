using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>Test seam for resolving a single Key Vault secret by name.</summary>
public interface IKeyVaultSecretResolver
{
    string? Resolve(string secretName);
}
