using Microsoft.Extensions.Configuration;
using Shouldly;
using VertexBPMN.Application.Configuration;

namespace VertexBPMN.Tests.Integration.Configuration;

public class ConfigurationSecretProviderTests
{
    [Fact]
    public void GetSecret_ShouldPreferConfiguredValueOverEnvironmentFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:OpenAI:ApiKey"] = "configured-key"
            })
            .Build();
        var environmentVariable = "VERTEXBPMN_TEST_SECRET_PRIORITY";
        var previousValue = Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "environment-key");
            var provider = new ConfigurationSecretProvider(configuration);

            provider.GetSecret("AI:OpenAI:ApiKey", environmentVariable).ShouldBe("configured-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    [Fact]
    public void GetSecret_ShouldUseEnvironmentFallbackWhenConfigurationIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environmentVariable = "VERTEXBPMN_TEST_SECRET_FALLBACK";
        var previousValue = Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "environment-key");
            var provider = new ConfigurationSecretProvider(configuration);

            provider.GetSecret("AI:OpenAI:ApiKey", environmentVariable).ShouldBe("environment-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }
}