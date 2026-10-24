using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Portal.Bff;

namespace VertexBPMN.Tests.Infrastructure;

/// <summary>Boots the portal host (VertexBPMN.Portal.Program) with an in-memory gateway and test identity.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit test fixture retained for framework activation and injection into public test constructors.")]
public sealed class PortalWebApplicationFactory : WebApplicationFactory<VertexBPMN.Portal.Program>
{
    internal FakeBffRuntimeGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IBffRuntimeGateway>(Gateway);
            services.AddAuthentication(PortalTestAuthHandler.AuthenticationSchemeName)
                .AddScheme<AuthenticationSchemeOptions, PortalTestAuthHandler>(PortalTestAuthHandler.AuthenticationSchemeName, _ => { });
        });
    }

    /// <summary>A released-application content with a sanctioned data-source-read binding.</summary>
    public static string ReleasedContent(string dataSourceKey = "invoiceList")
        => $$"""
        {
          "dataSources": [ { "key": "{{dataSourceKey}}", "bindingVersion": 2 } ],
          "pages": [
            {
              "id": "home",
              "route": "/home",
              "actions": [ { "type": "data-source-read", "version": "1.0.0", "dataSource": "{{dataSourceKey}}" } ],
              "components": [ { "type": "table", "version": "1.0.0", "dataSource": "{{dataSourceKey}}" } ]
            },
            { "id": "settings", "route": "/settings", "components": [ { "type": "text", "version": "1.0.0" } ] }
          ]
        }
        """;
}
