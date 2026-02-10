# .NET 10.0 Upgrade Tasks for VertexBPMN
**Progress**: 8/8 tasks complete (100%) ![100%](https://progress-bar.xyz/100)
## Execution Rules
- Execute tasks in order.
- Mark tasks and actions as complete only after verification passes.
- Stop on failures and report details.

## Tasks

### [✓] TASK-001: Verify prerequisites and repository state *(Completed: 2026-02-09 17:09)*
- [✓] (1) Confirm `.NET 10.0` SDK is installed and compatible with the solution.
- [✓] (2) Review `global.json` (if present) and update to allow `.NET 10.0`.
- [✓] (3) Verify repo has no pending changes and is on branch `upgrade-to-NET10`.
- [✓] (4) Document verification results.

### [✓] TASK-002: Update target frameworks to `net10.0` *(Completed: 2026-02-09 17:15)*
- [✓] (1) Update `TargetFramework` to `net10.0` for all `net9.0` projects.
- [✓] (2) Keep `utils/VertexBPMN.EntityGenerator/VertexBPMN.EntityGenerator.csproj` on `netstandard2.0`.
- [✓] (3) Restore packages and confirm project files parse correctly.

### [✓] TASK-003: Update NuGet package versions per plan *(Completed: 2026-02-09 17:20)*
- [✓] (1) Update AspNetCore packages to `10.0.2` where recommended.
- [✓] (2) Update EF Core packages to `10.0.2` where recommended.
- [✓] (3) Update Microsoft.Extensions packages to `10.0.2` where recommended.
- [✓] (4) Update `System.Diagnostics.PerformanceCounter` to `10.0.2` where recommended.
- [✓] (5) Restore packages and verify no downgrade warnings.

### [✓] TASK-004: Remove obsolete package from `VertexBPMN.EntityGenerator` *(Completed: 2026-02-09 17:23)*
- [✓] (1) Remove `System.ComponentModel.Annotations` package reference.
- [✓] (2) Restore packages and ensure the project builds.

### [✓] TASK-005: Address API breaking changes and behavioral updates *(Completed: 2026-02-09 17:40)*
- [✓] (1) Resolve `JwtSecurityTokenHandler` and JWT bearer API changes in `VertexBPMN.McpAdapter`.
- [✓] (2) Review and update `TimeSpan.From*` overload usage flagged by assessment.
- [✓] (3) Review usage of `System.Uri`, `HttpContent`, `JsonDocument`, and other behavioral changes flagged by assessment.
- [✓] (4) Confirm all code changes compile.

### [✓] TASK-006: Build solution on .NET 10 *(Completed: 2026-02-09 17:57)*
- [✓] (1) Run `dotnet build` for the solution.
- [✓] (2) Resolve build errors if any arise.
- [✓] (3) Confirm build succeeds with zero errors.

### [✓] TASK-007: Execute tests and validate runtime behavior *(Completed: 2026-02-09 18:04)*
- [✓] (1) Run tests for `VertexBPMN.Tests`, `VertexBPMN.Test.Parsing`, `PerformanceRunner`, and `TestRunner`.
- [✓] (2) Resolve any failing tests.
- [✓] (3) Confirm all test runs succeed.

### [✓] TASK-008: Final verification and documentation *(Completed: 2026-02-09 18:04)*
- [✓] (1) Re-run restore/build to confirm clean state.
- [✓] (2) Update `execution_log.md` with summary of changes and verification.
- [✓] (3) Confirm `plan.md` and `tasks.md` reflect completion state.

Commit: d60d9cb
Branch: upgrade-to-NET10
Message: Upgrade to .NET 10.0

31 files changed, 11535 insertions(+), 90 deletions(-)

[408aca9](https://github.com/CrawfordSystems/VertexBPMN/commit/408aca9) - Fix performance tests for .NET 10 compatibility
