using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Security;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class OidcTestProfileSecurityTests
{
    [Fact]
    public void OidcTest_AllowsHttpMetadataOnlyForLoopbackAuthority()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProductionSecurity(Configuration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "OidcTest",
            ["Jwt:Authority"] = "http://localhost:58080/realms/vertexbpmn",
            ["Jwt:Audience"] = "vertexbpmn-api",
            ["Jwt:ClockSkewSeconds"] = "0",
            ["Jwt:MetadataRefreshIntervalSeconds"] = "1",
            ["Jwt:RequireHttpsMetadata"] = "false",
            ["Jwt:UseDevelopmentApiKey"] = "true"
        }));

        using var provider = services.BuildServiceProvider();
        var authentication = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Authentication.AuthenticationOptions>>().Value;
        var jwt = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authentication.DefaultAuthenticateScheme);
        Assert.False(jwt.RequireHttpsMetadata);
        Assert.Equal("http://localhost:58080/realms/vertexbpmn", jwt.Authority);
        Assert.Equal(TimeSpan.Zero, jwt.TokenValidationParameters.ClockSkew);
        Assert.True(jwt.RefreshOnIssuerKeyNotFound);
        Assert.Equal(TimeSpan.FromSeconds(1), jwt.RefreshInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void InvalidMetadataRefreshInterval_IsRejected(int seconds)
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["Jwt:Authority"] = "https://identity.example.org/realms/vertexbpmn",
            ["Jwt:Audience"] = "vertexbpmn-api",
            ["Jwt:MetadataRefreshIntervalSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddProductionSecurity(configuration));

        Assert.Contains("MetadataRefreshIntervalSeconds", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(301)]
    public void InvalidClockSkew_IsRejected(int seconds)
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["Jwt:Authority"] = "https://identity.example.org/realms/vertexbpmn",
            ["Jwt:Audience"] = "vertexbpmn-api",
            ["Jwt:ClockSkewSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddProductionSecurity(configuration));

        Assert.Contains("ClockSkewSeconds", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", "http://localhost:58080/realms/vertexbpmn")]
    [InlineData("OidcTest", "http://identity.example.org/realms/vertexbpmn")]
    [InlineData("OidcTest", "https://identity.example.org/realms/vertexbpmn")]
    public void InsecureMetadata_IsRejectedOutsideLocalOidcTest(string mode, string authority)
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = mode,
            ["Jwt:Authority"] = authority,
            ["Jwt:Audience"] = "vertexbpmn-api",
            ["Jwt:RequireHttpsMetadata"] = "false"
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddProductionSecurity(configuration));

        Assert.Contains("RequireHttpsMetadata", error.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
