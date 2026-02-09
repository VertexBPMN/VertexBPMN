# .NET 10.0 Upgrade Plan for VertexBPMN

## Table of Contents
- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Implementation Timeline](#implementation-timeline)
- [Project-by-Project Plans](#project-by-project-plans)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Risk Management](#risk-management)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

## Executive Summary
This plan upgrades VertexBPMN from `.NET 9.0` to `.NET 10.0 (LTS)` across all projects.

### Discovered Metrics
- **Projects**: 18 total (17 targeting `net9.0`, 1 targeting `netstandard2.0`)
- **Issues**: 328 total (28 mandatory, 300 potential)
- **Affected files**: 89
- **Total LOC**: 124,014
- **Dependency depth**: 6 levels (deep dependency chain ending at `VertexBPMN.Tests`)
- **Packages**: 70 total, 21 with recommended upgrades
- **Key feature flags**: IdentityModel & Claims-based Security (3 occurrences, `VertexBPMN.McpAdapter`)

### Complexity Classification
**Complex** — 18 projects, deep dependency chain, and high issue count. This increases coordination risk, but the upgrade will still be executed as a unified operation per the selected strategy.

### Selected Strategy
**All-At-Once Strategy** — All projects upgraded simultaneously in a single coordinated update.

**Rationale**:
- Explicit requirement to upgrade to `.NET 10.0` across the solution
- All projects already on modern `net9.0` (or `netstandard2.0`) and SDK-style
- Package upgrade paths are defined for all recommended updates

### Iteration Plan
This plan will be completed in staged detail passes:
- Phase 1: Dependency analysis and strategy alignment
- Phase 2: Project stubs + risk/complexity overview
- Phase 3: Detailed project plans and package tables
- Final: Source control and success criteria

## Migration Strategy
### Selected Approach
**All-At-Once Strategy** — All projects upgraded simultaneously to `.NET 10.0` in a single coordinated operation.

**Justification**:
- Target framework upgrade is consistent across 17 `net9.0` projects with one `netstandard2.0` library that remains compatible
- Package upgrade guidance is explicit for all recommended updates
- Solution is fully SDK-style, which simplifies unified changes

### Ordering Principles
- **Atomic upgrade across all projects**: Target framework and package updates applied together, no intermediate mixed-target states.
- **Dependency awareness**: Validation and troubleshooting prioritize foundational projects first (Levels 0–2), but changes are applied across the entire solution simultaneously.
- **Test projects last**: Test execution happens after the full solution builds on `.NET 10.0`.

### Special Considerations
- `VertexBPMN.EntityGenerator` remains on `netstandard2.0` unless assessment indicates a target change. It will be validated for compatibility.
- IdentityModel & claims-based security APIs are flagged in `VertexBPMN.McpAdapter` and require focused review.

## Implementation Timeline
### Phase 0: Preparation
- Confirm `.NET 10.0` SDK availability (and update `global.json` if present).

### Phase 1: Atomic Upgrade
**Operations (single coordinated batch):**
- Update **all project files** to target frameworks (`net10.0` or `netstandard2.0` where specified)
- Update **all NuGet packages** with recommended versions
- Restore dependencies and build the full solution
- Fix all compilation issues discovered from API changes

**Deliverable**: Solution builds with zero errors under `.NET 10.0`.

### Phase 2: Test Validation
- Execute all test projects (`VertexBPMN.Tests`, `VertexBPMN.Test.Parsing`, `PerformanceRunner`, `TestRunner`)
- Resolve test failures and re-run

**Deliverable**: All tests pass on `.NET 10.0`.

## Detailed Dependency Analysis
### Dependency Graph Summary
The solution has a 6-level dependency chain. While the upgrade is executed atomically, understanding the dependency hierarchy ensures correct validation order and highlights foundational libraries.

**Level 0 (foundation, no dependencies):**
- `VertexBPMN.Domain`
- `VertexBPMN.EntityGenerator` (netstandard2.0)
- `VertexBPMN.McpClient`
- `VertexBPMN.Model.Schema`
- `VertexBPMN.Parsing`

**Level 1:**
- `VertexBPMN.Application` (depends on `VertexBPMN.Domain`)
- `VertexBPMN.Model` (depends on `VertexBPMN.EntityGenerator`)
- `VertexBPMN.Studio` (depends on `VertexBPMN.Domain`)

**Level 2:**
- `VertexBPMN.Infrastructure` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`)

**Level 3:**
- `VertexBPMN.Engine` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`)

**Level 4:**
- `VertexBPMN.Api` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`, `VertexBPMN.Engine`)
- `VertexBPMN.Benchmarks` (depends on `VertexBPMN.Domain`, `VertexBPMN.Engine`)
- `VertexBPMN.Test.Parsing` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`, `VertexBPMN.Engine`)
- `PerformanceRunner` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Engine`)
- `TestRunner` (depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Engine`)

**Level 5:**
- `VertexBPMN.McpAdapter` (depends on `VertexBPMN.Api`)
- `VertexBPMN.McpAgentPlugin` (depends on `VertexBPMN.Api`)

**Level 6 (top-level tests):**
- `VertexBPMN.Tests` (depends on `VertexBPMN.Application`, `VertexBPMN.McpClient`, `VertexBPMN.McpAgentPlugin`, `VertexBPMN.Api`, `VertexBPMN.Domain`, `VertexBPMN.Infrastructure`, `VertexBPMN.Studio`, `VertexBPMN.McpAdapter`, `VertexBPMN.Engine`)

### Critical Path
`VertexBPMN.Domain` ? `VertexBPMN.Application` ? `VertexBPMN.Infrastructure` ? `VertexBPMN.Engine` ? `VertexBPMN.Api` ? `VertexBPMN.McpAdapter`/`VertexBPMN.McpAgentPlugin` ? `VertexBPMN.Tests`

### Circular Dependencies
None detected in the assessment graph.

## Project-by-Project Plans
### Project: `benchmarks/VertexBPMN.Benchmarks/VertexBPMN.Benchmarks.csproj`
- **Current State**: `net9.0`, DotNetCoreApp, depends on `VertexBPMN.Domain`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Review behavioral change APIs if used in benchmarks (`HttpContent`, `Uri`).
  3. **Validation**: builds without errors; benchmarks run under `.NET 10.0`.

### Project: `src/VertexBPMN.Api/VertexBPMN.Api.csproj`
- **Current State**: `net9.0`, AspNetCore, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Update NuGet packages:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.AspNetCore.OpenApi` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.Design` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.InMemory` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `System.Diagnostics.PerformanceCounter` | 9.0.9 | 10.0.2 | Framework compatibility |
  3. Review source-incompatible JWT bearer APIs (`JwtBearerOptions` changes) and `System.Diagnostics.PerformanceCounter` changes.
  4. Validate Minimal API registrations and OpenAPI generation under `.NET 10.0`.
  5. **Validation**: builds without errors; API integration tests pass.

### Project: `src/VertexBPMN.Application/VertexBPMN.Application.csproj`
- **Current State**: `net9.0`, ClassLibrary, depends on `VertexBPMN.Domain`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Update NuGet packages:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `Microsoft.Extensions.Hosting.Abstractions` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.Extensions.Http` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.Extensions.Logging` | 9.0.9 | 10.0.2 | Framework compatibility |
  3. Review source-incompatible API calls flagged in assessment (`TimeSpan.From*`, configuration binder changes).
  4. **Validation**: builds without errors; dependent projects compile.

### Project: `src/VertexBPMN.Domain/VertexBPMN.Domain.csproj`
- **Current State**: `net9.0`, ClassLibrary, no project dependencies
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Update NuGet packages:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `Microsoft.EntityFrameworkCore` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.Extensions.Diagnostics.HealthChecks` | 9.0.9 | 10.0.2 | Framework compatibility |
  3. Review source-incompatible API calls flagged in assessment (notably `TimeSpan.From*` overloads).
  4. Validate EF Core model compatibility with .NET 10 runtime.
  5. Build project and run any dependent unit tests.
  6. **Validation**: builds without errors; no package downgrade conflicts.

### Project: `src/VertexBPMN.Engine/VertexBPMN.Engine.csproj`
- **Current State**: `net9.0`, ClassLibrary, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Update NuGet packages:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `Microsoft.Extensions.Logging` | 9.0.9 | 10.0.2 | Framework compatibility |
  3. Review behavioral/source changes for `System.Net.Http.HttpContent`, `System.Uri`, and `TimeSpan.From*` overloads.
  4. **Validation**: builds without errors; core engine tests pass.

### Project: `src/VertexBPMN.Infrastructure/VertexBPMN.Infrastructure.csproj`
- **Current State**: `net9.0`, ClassLibrary, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Update NuGet packages:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `Microsoft.EntityFrameworkCore` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.Design` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.InMemory` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.Relational` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.EntityFrameworkCore.SqlServer` | 9.0.9 | 10.0.2 | Framework compatibility |
     | `Microsoft.Extensions.DependencyInjection` | 9.0.9 | 10.0.2 | Framework compatibility |
  3. Review binary-incompatible API calls flagged in assessment (configuration binder/DI APIs).
  4. Validate EF Core provider configurations (PostgreSQL/SQL Server).
  5. **Validation**: builds without errors; EF Core migrations compile.

### Project: `src/VertexBPMN.Integration/McpAdapter/VertexBPMN.McpAdapter.csproj`
- **Current State**: `net9.0`, AspNetCore, depends on `VertexBPMN.Api`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Review IdentityModel & claims-based security changes:
     - `JwtSecurityTokenHandler` APIs flagged as binary incompatible
     - Validate token validation behavior and exception handling
  3. Review behavioral changes for `System.Net.Http.HttpContent` and `System.Uri` in adapter endpoints.
  4. **Validation**: builds without errors; adapter integration tests pass.

### Project: `src/VertexBPMN.Integration/McpAgentPlugin/VertexBPMN.McpAgentPlugin.csproj`
- **Current State**: `net9.0`, ClassLibrary, depends on `VertexBPMN.Api`
- **Target State**: `net10.0`
- **Migration Steps**:
  1. Update `TargetFramework` to `net10.0`.
  2. Review behavioral change API usage flagged in assessment.
  3. **Validation**: builds without errors; plugin loads in dependent tests.

### Project: `tests/PerformanceRunner/PerformanceRunner.csproj`
- **Current State**: `net9.0`, DotNetCoreApp, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**: [Details to be filled]

### Project: `tests/TestRunner/TestRunner.csproj`
- **Current State**: `net9.0`, DotNetCoreApp, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**: [Details to be filled]

### Project: `tests/VertexBPMN.Test.Parsing/VertexBPMN.Test.Parsing.csproj`
- **Current State**: `net9.0`, DotNetCoreApp, depends on `VertexBPMN.Domain`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**: [Details to be filled]

### Project: `tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj`
- **Current State**: `net9.0`, DotNetCoreApp, depends on `VertexBPMN.Application`, `VertexBPMN.McpClient`, `VertexBPMN.McpAgentPlugin`, `VertexBPMN.Api`, `VertexBPMN.Domain`, `VertexBPMN.Infrastructure`, `VertexBPMN.Studio`, `VertexBPMN.McpAdapter`, `VertexBPMN.Engine`
- **Target State**: `net10.0`
- **Migration Steps**: [Details to be filled]

### Project: `utils/VertexBPMN.EntityGenerator/VertexBPMN.EntityGenerator.csproj`
- **Current State**: `netstandard2.0`, ClassLibrary, no project dependencies
- **Target State**: `netstandard2.0` (remain compatible)
- **Migration Steps**:
  1. Keep `TargetFramework` at `netstandard2.0`.
  2. Remove NuGet packages included by framework reference:
     | Package | Current | Target | Reason |
     | --- | --- | --- | --- |
     | `System.ComponentModel.Annotations` | 5.0.0 | Remove | Included in framework reference |
  3. Validate build and ensure `VertexBPMN.Model` still compiles.
  4. **Validation**: no package reference conflicts; project builds.

## Risk Management
### High-Risk Areas
| Project | Risk Level | Rationale | Mitigation |
| --- | --- | --- | --- |
| `VertexBPMN.Model.Schema` | High | Large codebase (~36k LOC) with behavioral API changes | Prioritize compilation review and full test pass on schema-related tests |
| `VertexBPMN.Model` | High | Large codebase (~18k LOC) with behavioral API changes | Validate serialization and model compatibility after upgrade |
| `VertexBPMN.Engine` | High | Core engine (~14k LOC) with multiple API change categories | Run core engine tests and integration flows after upgrade |

### Medium-Risk Areas
- `VertexBPMN.Api`, `VertexBPMN.Application`, `VertexBPMN.Infrastructure`, `VertexBPMN.Tests`

### IdentityModel Security APIs
- `VertexBPMN.McpAdapter` uses IdentityModel APIs flagged for changes; review token validation logic and package usage post-upgrade.

## Complexity & Effort Assessment
| Project | Complexity | Notes |
| --- | --- | --- |
| `VertexBPMN.Domain` | Low | Foundation library with minimal API changes |
| `VertexBPMN.Application` | Medium | Moderate API changes and package upgrades |
| `VertexBPMN.Infrastructure` | Medium | Package upgrades for EF Core and hosting stack |
| `VertexBPMN.Engine` | High | Core engine; multiple API change categories |
| `VertexBPMN.Api` | Medium | AspNetCore app with package upgrades and API changes |
| `VertexBPMN.Model.Schema` | High | Large schema project with behavioral changes |
| `VertexBPMN.Model` | High | Large model project with behavioral changes |
| `VertexBPMN.Parsing` | Medium | Parser logic, some API changes |
| `VertexBPMN.Studio` | Medium | AspNetCore app with package upgrades |
| `VertexBPMN.McpAdapter` | Medium | IdentityModel changes, behavioral changes |
| `VertexBPMN.McpAgentPlugin` | Low | Small plugin library |
| `VertexBPMN.McpClient` | Low | Small client library |
| `VertexBPMN.EntityGenerator` | Low | netstandard library, minimal change |
| `VertexBPMN.Tests` | Medium | High test surface, behavioral changes |
| `VertexBPMN.Test.Parsing` | Medium | Test suite with API changes |
| `PerformanceRunner` | Low | Benchmark runner with minimal changes |
| `TestRunner` | Low | Test harness with minimal changes |
| `VertexBPMN.Benchmarks` | Low | Benchmark project with minimal changes |
