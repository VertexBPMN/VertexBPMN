# Production Hardening Baseline

Captured for the prioritized VertexBPMN production-hardening work.

## Repository state

- Branch: `master`
- Upstream: `origin/master`
- Working tree at capture time: clean
- Solution: `VertexBPMN.sln`
- Main target framework: .NET 10
- Generator compatibility target: .NET Standard 2.0

The previously observed local parser, serializer, security, Studio, test, and project-file modifications were no longer present when implementation began. No local changes were reset or discarded by this work.

## Reproduction

```powershell
dotnet build .\VertexBPMN.sln
dotnet test .\VertexBPMN.sln --no-build --verbosity quiet
```

## Build baseline

The complete solution builds successfully.

## Test baseline

| Test assembly | Result |
| --- | --- |
| `VertexBPMN.Test.Parsing` | 231 passed |
| `PerformanceRunner` | passed |
| `VertexBPMN.Tests` | 35 failed, 4 skipped |
| Complete solution | 466 total, 427 passed, 35 failed, 4 skipped |

The baseline run exits with Microsoft Testing Platform non-success exit code 2.

## Scoped workstreams

1. Parser contracts and strict parser/serializer roundtrip.
2. BPMN token execution and conformance semantics.
3. API persistence, health checks, and deterministic test isolation.
4. DMN behavior and JSON boundary handling.
5. MCP/AI relevance, hermetic integration tests, and optional live tests.
6. Product-reachable simulated implementations directly touched by these workstreams.

Unrelated feature requests and cosmetic TODO comments are excluded from this hardening effort.

## Change classification

Future changes are to be reviewed in these groups:

- Parser/serializer model and strict-roundtrip behavior.
- XML security and resource limits.
- Runtime execution and conformance tests.
- API, persistence, tenancy, and test infrastructure.
- DMN, MCP, and AI integrations.
- Blazor Studio only where required by changed contracts.
- Package/framework updates only when required by a root-cause fix.
