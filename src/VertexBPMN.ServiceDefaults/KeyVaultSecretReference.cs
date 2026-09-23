using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Configuration provider that resolves environment-variable values shaped like
/// <c>secretref:&lt;KeyVaultSecretName&gt;</c> into the actual Key Vault secret
/// value at startup, using the host's managed identity (DefaultAzureCredential,
/// which honours the user-assigned identity via AZURE_CLIENT_ID).
///
/// This replaces the cancelled Azure Container Apps <c>secretref:</c> env-var
/// interpolation (which was never resolving) with explicit, app-side resolution
/// so Stage/Production can keep real connection strings and signing keys out of
/// env vars / the repository, in line with the MI-only, Key Vault-first policy.
/// </summary>
public static class KeyVaultSecretReferenceExtensions
{
    /// <summary>
    /// Adds a configuration source that resolves <c>secretref:&lt;name&gt;</c>
    /// tokens found in any environment variable into the Key Vault secret value.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="keyVaultUri">Optional explicit vault URI; otherwise read from
    /// configuration key <c>KeyVault:Uri</c>.</param>
    /// <param name="resolver">Optional secret resolver override (tests).</param>
    public static TBuilder AddKeyVaultSecretReferences<TBuilder>(
        this TBuilder builder, string? keyVaultUri = null, IKeyVaultSecretResolver? resolver = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Configuration.Add(new KeyVaultSecretReferenceSource(() =>
        {
            // The vault URI must be available without resolving secrets itself.
            var uri = keyVaultUri
                ?? builder.Configuration["KeyVault:Uri"]
                ?? Environment.GetEnvironmentVariable("KeyVault__Uri");
            return uri;
        }, resolver));
        return builder;
    }

    private sealed class KeyVaultSecretReferenceSource : IConfigurationSource
    {
        private readonly Func<string?> _vaultUri;
        private readonly IKeyVaultSecretResolver? _resolver;

        public KeyVaultSecretReferenceSource(Func<string?> vaultUri, IKeyVaultSecretResolver? resolver)
        {
            _vaultUri = vaultUri;
            _resolver = resolver;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new KeyVaultSecretReferenceProvider(_vaultUri(), _resolver);
    }

    private sealed class KeyVaultSecretReferenceProvider : ConfigurationProvider
    {
        private readonly string? _vaultUri;
        private readonly IKeyVaultSecretResolver? _resolver;

        public KeyVaultSecretReferenceProvider(string? vaultUri, IKeyVaultSecretResolver? resolver)
        {
            _vaultUri = vaultUri;
            _resolver = resolver;
        }

        public override void Load()
        {
            if (string.IsNullOrWhiteSpace(_vaultUri))
                return; // local/dev: no vault configured; leave values untouched

            var pending = ParseSecretRefTokens(Environment.GetEnvironmentVariables());
            if (pending.Count == 0)
                return;

            var resolver = _resolver ?? new AzureKeyVaultSecretResolver(_vaultUri!);

            foreach (var (configKey, secretName) in pending)
            {
                try
                {
                    var value = resolver.Resolve(secretName);
                    if (value is not null)
                        Set(configKey, value);
                }
                catch (Exception)
                {
                    // Leave the unresolved token in place; downstream config
                    // validation will surface a clear error naming the secret.
                }
            }
        }
    }

    /// <summary>
    /// Scans the given environment variables for <c>secretref:&lt;name&gt;</c>
    /// values and returns the config-key → secret-name pairs to resolve.
    /// Exposed for unit testing; behaviour is a pure function of the input.
    /// </summary>
    internal static IReadOnlyList<(string ConfigKey, string SecretName)> ParseSecretRefTokens(
        System.Collections.IDictionary env)
    {
        var pending = new List<(string ConfigKey, string SecretName)>();
        foreach (System.Collections.DictionaryEntry entry in env)
        {
            var name = entry.Key?.ToString();
            var value = entry.Value?.ToString();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                continue;
            if (!value.StartsWith("secretref:", StringComparison.Ordinal))
                continue;

            var secretName = value.Substring("secretref:".Length).Trim();
            if (string.IsNullOrEmpty(secretName))
                continue;

            // env var KEY__SUB maps to config KEY:SUB
            var configKey = name.Replace("__", ":", StringComparison.Ordinal);
            pending.Add((configKey, secretName));
        }

        return pending;
    }

    /// <summary>Default resolver backed by the real Azure Key Vault SDK + managed identity.</summary>
    private sealed class AzureKeyVaultSecretResolver : IKeyVaultSecretResolver
    {
        private readonly SecretClient _client;

        public AzureKeyVaultSecretResolver(string vaultUri)
            => _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());

        public string? Resolve(string secretName)
            => _client.GetSecret(secretName).Value?.Value;
    }
}

/// <summary>Test seam for resolving a single Key Vault secret by name.</summary>
public interface IKeyVaultSecretResolver
{
    string? Resolve(string secretName);
}
