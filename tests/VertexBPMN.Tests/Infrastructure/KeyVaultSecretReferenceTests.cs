using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace VertexBPMN.Tests.Infrastructure;

public class KeyVaultSecretReferenceTests
{
    private sealed class FakeResolver : IKeyVaultSecretResolver
    {
        public string? Resolve(string secretName)
            => secretName switch
            {
                "pg-bpmn" => "Server=pg;Database=bpmn",
                "jwt-secret-key" => "0123456789abcdef0123456789abcdef",
                "missing" => null,
                _ => "resolved:" + secretName
            };
    }

    [Fact]
    public void Parse_Returns_Only_SecretRef_Tokens_With_ConfigKey_Mapping()
    {
        var env = new Hashtable
        {
            ["ConnectionStrings__BpmnDbContext"] = "secretref:pg-bpmn",
            ["Jwt__SecretKey"] = "secretref:jwt-secret-key",
            ["OperationalMode"] = "Stage",           // not a token -> ignored
            ["ConnectionStrings__Plain"] = "Server=x", // not a token -> ignored
            ["EmptyRef"] = "secretref:",              // empty secret name -> ignored
            [""] = "secretref:pg-bpmn",               // empty key -> ignored
        };

        var result = KeyVaultSecretReferenceExtensions.ParseSecretRefTokens(env);

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.ConfigKey == "ConnectionStrings:BpmnDbContext" && r.SecretName == "pg-bpmn");
        result.ShouldContain(r => r.ConfigKey == "Jwt:SecretKey" && r.SecretName == "jwt-secret-key");
    }

    [Fact]
    public void Configured_Provider_Resolves_Tokens_Through_Resolver()
    {
        WithEnv("ConnectionStrings__BpmnDbContext", "secretref:pg-bpmn", () =>
        {
            var builder = new ConfigurationBuilder();
            builder.AddEnvironmentVariables(); // host adds an env-var source by default
            builder.Add(new TestKeyVaultSecretResolverSource("https://test.vault.azure.net", new FakeResolver()));

            var config = builder.Build();

            config["ConnectionStrings:BpmnDbContext"].ShouldBe("Server=pg;Database=bpmn");
        });
    }

    [Fact]
    public void No_Vault_Uri_Leaves_Values_Untouched()
    {
        WithEnv("ConnectionStrings__BpmnDbContext", "secretref:pg-bpmn", () =>
        {
            var builder = new ConfigurationBuilder();
            builder.AddEnvironmentVariables(); // host adds an env-var source by default
            builder.Add(new TestKeyVaultSecretResolverSource(string.Empty, new FakeResolver()));

            var config = builder.Build();

            // No vault configured => token stays as-is (local/dev behaviour).
            config["ConnectionStrings:BpmnDbContext"].ShouldBe("secretref:pg-bpmn");
        });
    }

    [Fact]
    public void Missing_Secret_Leaves_Token_In_Place()
    {
        WithEnv("ConnectionStrings__X", "secretref:missing", () =>
        {
            var builder = new ConfigurationBuilder();
            builder.AddEnvironmentVariables(); // host adds an env-var source by default
            builder.Add(new TestKeyVaultSecretResolverSource("https://test.vault.azure.net", new FakeResolver()));

            var config = builder.Build();

            // Null secret => token is not overwritten.
            config["ConnectionStrings:X"].ShouldBe("secretref:missing");
        });
    }

    private static void WithEnv(string key, string value, Action body)
    {
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, value);
            body();
        }
        finally
        {
            // Restore the prior value (or clear it if it was unset).
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    // Test seam mirroring the private Source/Provider of the production code,
    // with the secret-resolution dependency injectable.
    private sealed class TestKeyVaultSecretResolverSource : IConfigurationSource
    {
        private readonly string _vaultUri;
        private readonly IKeyVaultSecretResolver _resolver;

        public TestKeyVaultSecretResolverSource(string vaultUri, IKeyVaultSecretResolver resolver)
        {
            _vaultUri = vaultUri;
            _resolver = resolver;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new TestKeyVaultSecretResolverProvider(_vaultUri, _resolver);
    }

    private sealed class TestKeyVaultSecretResolverProvider : ConfigurationProvider
    {
        private readonly string _vaultUri;
        private readonly IKeyVaultSecretResolver _resolver;

        public TestKeyVaultSecretResolverProvider(string vaultUri, IKeyVaultSecretResolver resolver)
        {
            _vaultUri = vaultUri;
            _resolver = resolver;
        }

        public override void Load()
        {
            if (string.IsNullOrWhiteSpace(_vaultUri))
                return;

            var pending = KeyVaultSecretReferenceExtensions.ParseSecretRefTokens(Environment.GetEnvironmentVariables());
            foreach (var (configKey, secretName) in pending)
            {
                try
                {
                    var value = _resolver.Resolve(secretName);
                    if (value is not null)
                        Set(configKey, value);
                }
                catch (Exception)
                {
                    // leave token as-is
                }
            }
        }
    }
}
