# Security and release gates

## Pull requests and master

The `VertexBPMN CI` workflow treats the following jobs as blocking qualification evidence:

- Linux and Windows restore, build, Studio assets, complete green suite, BPMN and advanced-feature conformance contracts, OpenAPI snapshot and Kubernetes manifest validation;
- NuGet and npm audits with zero vulnerable NuGet entries and zero high or critical npm findings;
- CodeQL analysis for C# and JavaScript/TypeScript;
- filesystem dependency, secret and misconfiguration scanning with Trivy at `HIGH` and `CRITICAL` severity;
- measured line coverage of at least 60% and branch coverage of at least 45%;
- API and Studio container builds, high/critical container scans and separate SPDX JSON SBOMs;
- real RabbitMQ delivery and PostgreSQL migration qualification.

Pull requests additionally run GitHub dependency review and reject newly introduced dependencies at high or critical severity. Dependabot checks NuGet, Studio npm and GitHub Actions dependencies weekly.

Repository rules for `master` must require all non-skipped jobs above, including both CodeQL languages and `Operational integration (RabbitMQ and PostgreSQL)`. The ruleset is GitHub repository state and must be checked after the workflow is merged; it cannot be enforced by this file alone.

## Local security and quality checks

After restore, build and `npm ci`, run:

```text
bash scripts/verify-dependency-audit.sh
bash scripts/verify-coverage.sh
bash scripts/verify-openapi-snapshot.sh
bash scripts/verify-phase1-acceptance-baseline.sh
bash scripts/verify-phase4-acceptance.sh
```

Trivy, CodeQL, the SBOM action and the external RabbitMQ/PostgreSQL job are authoritative in GitHub Actions because they require their scanner runtime or service containers.

On the local Windows/WSL workstation, container builds use WSLC instead of Docker:

```text
wslc.exe build --tag vertexbpmn:release-check .
wslc.exe build --file src/VertexBPMN.Studio/Dockerfile --tag vertexbpmn-studio:release-check .
wslc.exe inspect --type image vertexbpmn:release-check
wslc.exe inspect --type image vertexbpmn-studio:release-check
```

Both images must expose port 8080, use the expected .NET entrypoint and run as the non-root `APP_UID`. GitHub-hosted CI continues to use Docker and Trivy because WSLC is a local Windows/WSL runtime, not a GitHub Runner dependency.

## Tagged releases

A pushed `v*` tag cannot publish directly. `Clean release qualification` waits for the complete build matrix, dependency audits, both CodeQL analyses, supply-chain gates and operational integration. It then checks out the tag with full history, rebuilds Studio and the complete solution, reruns API/Engine/Studio tests plus the OpenAPI and conformance gates, verifies an unchanged source tree and packs the SDK and CLI twice.

The package gate canonicalizes NuGet container metadata, uses stable entry ordering and timestamps, and requires both pack results to be byte-identical. It emits `SHA256SUMS`. The publish job downloads only these qualified artifacts, verifies their checksums, creates a GitHub build-provenance attestation, exchanges OIDC for a short-lived NuGet key and publishes both packages.

Local package qualification uses an unpublished version:

```text
bash scripts/verify-reproducible-packages.sh 1.0.0-local.1
```

Do not create or reuse a release tag until all required checks on its source commit are green. NuGet Trusted Publishing must identify repository `VertexBPMN/VertexBPMN`, workflow file `ci.yml`, and the profile creator stored as `NUGET_USER`.
