# SDK, CLI and release checklist

VertexBPMN publishes `VertexBPMN.Sdk` and the `VertexBPMN.Cli` .NET tool. A release is valid only when GitHub Actions produces the packages from the tagged clean checkout; locally packed files are qualification evidence, not publish inputs.

## Before tagging

1. Confirm that the source commit is on `master` and its required CI checks are green.
2. Confirm that the support matrix, README, OpenAPI snapshot, gRPC contract and protocol examples describe the same public scope.
3. Run the same restore, Release build and central `VertexBPMN.Tests` project used by GitHub Actions. Run browser, benchmark, audit, coverage, conformance, external-service and container checks from [Security and Release Gates](security-and-release-gates.md) when the release risk requires them.
4. Install the generated CLI package from an isolated local source and run `vertexbpmn --help`; help must not create or migrate a database.
5. Confirm that examples and configuration contain placeholders rather than credentials.
6. Choose a new SemVer version. Never move or reuse a published tag.

## Trusted Publishing configuration

The nuget.org policy must use repository owner `VertexBPMN`, repository `VertexBPMN`, workflow file `ci.yml`, and the NuGet profile creator configured as the GitHub secret `NUGET_USER`. No persistent `NUGET_API_KEY` secret is used; `NuGet/login` returns a short-lived key after the OIDC trust exchange.

## Release

```text
git tag v1.0.1
git push origin v1.0.1
```

The tag runs the same restore, Release build and central test project as `master`. The build job then creates SDK and CLI exactly once with the version from the tag and uploads them as one workflow artifact. The publish job downloads that artifact, exchanges GitHub OIDC for a short-lived NuGet key and publishes both packages with `--skip-duplicate`.

Afterward, verify both NuGet package pages and install the CLI into an empty tool path. A failed OIDC login indicates a mismatch in repository, workflow filename, NuGet policy owner/creator or `NUGET_USER`; do not replace Trusted Publishing with a long-lived API key.

The solution does not define a packable `VertexBPMN.Core` project. Do not publish a package under that name until the project and its compatibility policy exist explicitly.
