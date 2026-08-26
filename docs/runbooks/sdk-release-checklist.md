# SDK, CLI and release checklist

VertexBPMN publishes `VertexBPMN.Sdk` and the `VertexBPMN.Cli` .NET tool. A release is valid only when GitHub Actions produces the packages from the tagged clean checkout; locally packed files are qualification evidence, not publish inputs.

## Before tagging

1. Confirm that the source commit is on `master` and its required CI checks are green.
2. Confirm that the support matrix, README, OpenAPI snapshot, gRPC contract and protocol examples describe the same public scope.
3. Run the local restore/build/test, dependency audit, coverage, conformance, OpenAPI and reproducible-package commands from [Reproducible Build and Test](build-and-test.md), then build and inspect the API and Studio images with the WSLC commands in [Security and Release Gates](security-and-release-gates.md).
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

The tag first waits for all build, audit, CodeQL, coverage, secret, container and external integration gates. The clean release job then rebuilds and tests API, Engine and Studio, checks OpenAPI/conformance, verifies a clean source tree, creates each package twice and requires byte-identical output. The publish job verifies `SHA256SUMS`, creates a provenance attestation and publishes exactly those qualified SDK and CLI artifacts.

Afterward, verify both NuGet package pages, install the CLI into an empty tool path and check the GitHub Actions attestation. A failed OIDC login indicates a mismatch in repository, workflow filename, NuGet policy owner/creator or `NUGET_USER`; do not replace Trusted Publishing with a long-lived API key.

The solution does not define a packable `VertexBPMN.Core` project. Do not publish a package under that name until the project and its compatibility policy exist explicitly.
