# SDK and Release Checklist

The repository contains runnable protocol examples under `sdk-examples/` for C#, JavaScript, Java, and Python. Keep these examples aligned with the public REST/gRPC contracts whenever an endpoint changes.

Before publishing an SDK or NuGet package:

1. Restore and build the solution in Release mode.
2. Run the complete test suite with the repository's Microsoft Testing Platform runner.
3. Build the API container without pushing it.
4. Verify that examples contain placeholders rather than credentials.
5. Generate OpenAPI and gRPC client artifacts from the same version as the server.
6. Publish packages only from explicitly packable project files and attach symbols and source mapping.

For the first .NET client package, use:

```powershell
dotnet pack src/VertexBPMN.Sdk/VertexBPMN.Sdk.csproj --configuration Release --output artifacts/sdk-pack
```

The resulting package is `VertexBPMN.Sdk.1.0.0.nupkg`. It contains only the typed REST client and has no server, persistence, or credential-storage dependency.

Recommended local verification:

```powershell
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore
dotnet test VertexBPMN.sln --configuration Release --no-build
docker build -t vertexbpmn:release-check .
```

The current solution does not define a packable `VertexBPMN.Core` project. Do not publish a package under that name until the project and its API compatibility policy are added explicitly.
