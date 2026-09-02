# VertexBPMN – AI Agent Coding Instructions

This document guides AI coding agents to be productive in the VertexBPMN codebase. It merges project-specific conventions with actionable, up-to-date instructions. **Focus on these points for best results:**

## Project Overview

- **VertexBPMN** is a modular, high-performance BPMN 2.0, CMMN 1.4 & DMN 1.4 engine for .NET 9, inspired by Camunda but built natively for .NET and C# 13.
- The architecture is service-oriented: core services (e.g., `RepositoryService`, `RuntimeService`, `TaskService`) are accessed via interfaces (e.g., `IRepositoryService`).
- Persistence is abstracted: **never** use `DbContext` directly in business logic—always go through repository interfaces (e.g., `IProcessDefinitionRepository`).
- The engine is stateless; all state is persisted in the database or distributed cache.
- All I/O (DB, network) must be async (`async`/`await`, prefer `ValueTask` where appropriate).

## Developer Workflows

- **Build:** Use `dotnet build` in the repo root or solution folder.
- **Add projects:** Use `dotnet sln add <project-path>` to add new projects to the solution.
- **Testing:** (Planned) Use xUnit for unit/integration tests. Test projects should be named `*.Tests` and use Moq/NSubstitute for mocking, FluentAssertions for assertions.
- **NuGet:** Add dependencies via `dotnet add package <PackageName>`.
- **Documentation:** All public/internal types and members require XML documentation.

## Code & Architecture Conventions

- **Framework:** .NET 9, C# 13 features (primary constructors, `record struct`, collection literals, etc.).
- **Persistence:** EF Core 9, repository/unit-of-work pattern. No direct `DbContext` in business logic.
- **API:** ASP.NET Core Minimal APIs for REST, gRPC for RPC.
- **Observability:** Use OpenTelemetry for tracing, metrics, and logging.
- **Database:** Target PostgreSQL (primary), SQL Server (secondary). Write portable EF Core code.
- **Naming:**
  - Methods: `PascalCase`, async methods end with `Async` (e.g., `StartProcessByKeyAsync`).
  - Interfaces: `IPascalCase` (e.g., `IRuntimeService`).
  - Private fields: `_camelCase`.
- **Nullability:** `#nullable enable` is enforced. Avoid null-forgiving (`!`) operator.
- **Immutability:** Use `record`/`record struct` for DTOs and immutable models. Historical data (e.g., `HistoryEvent`) is immutable after creation.
- **Error Handling:** Use custom exceptions (e.g., `ProcessNotFoundException`). Never throw generic `Exception`/`SystemException`.
- **Logging:** Use `ILogger` (Microsoft.Extensions.Logging). No `Console.WriteLine`/`Debug.WriteLine` in app logic.
- **No magic strings:** Use `nameof()` and `const`/`static readonly` for repeated strings (e.g., variable names, error codes).
- **No direct dependencies on concrete classes:** Always code against interfaces in public APIs.

## Domain-Specific Patterns

- **Token-based execution:** The engine models BPMN token flow. Think in terms of tokens moving through the process graph.
- **Transactional boundaries:** State changes must be atomic within DB transactions. Each "unit of work" (e.g., moving a token) is a single transaction.
- **Statelessness:** API and worker nodes must not hold process state in memory between requests.

## Testing & Quality

- **Test coverage:** Target ≥95% for core logic. All new features require unit/integration tests.
- **Test structure:** Use Arrange-Act-Assert (AAA) in all tests.
- **End-to-end/integration:** Test cross-component flows (API → Service → DB) as integration tests.
- **Conformance:** Code must pass BPMN MIWG and DMN TCK test suites.
- **TDD:** Schreibe zuerst einen fehlgeschlagenen Test (Red), dann minimalen Code (Green), anschließend gezieltes Refactoring (Refactor) – keine produktive Logik ohne vorgelagerten Test.

# VertexBPMN vs. Camunda REST-API: Paritätsmatrix

| Camunda Endpoint                | VertexBPMN-Status | Bemerkung / Mapping / TODO |
|---------------------------------|-------------------|---------------------------|
| /process-definition             | Teilweise         | RepositoryController, aber Filter/Paging/DTOs fehlen |
| /process-instance               | Teilweise         | RuntimeController, aber Camunda-typische Query/DTOs fehlen |
| /task                           | Teilweise         | TaskController, aber Filter, Paging, DTOs, Variablen fehlen |
| /history/task                   | Teilweise         | HistoryController, aber Query/DTOs fehlen |
| /deployment                     | Teilweise         | RepositoryController, aber Camunda-typische Response fehlt |
| /message                        | Fehlend           | Muss ergänzt werden |
| /signal                         | Fehlend           | Muss ergänzt werden |
| /job                            | Fehlend           | Muss ergänzt werden |
| /incident                       | Fehlend           | Muss ergänzt werden |
| /variable                       | Fehlend           | Muss ergänzt werden |
| /user, /group, /authorization   | Teilweise         | IdentityController, aber Camunda-typische Endpunkte fehlen |
| /filter                         | Fehlend           | Muss ergänzt werden |
| /decision-definition            | Teilweise         | DecisionController, aber Camunda-typische Query/DTOs fehlen |
| /decision-instance              | Fehlend           | Muss ergänzt werden |

## Absolute No-Gos

- No direct `DbContext` outside repository implementations.
- No manual thread creation; prefer async/await and TPL.
- No for-loops where LINQ is clearer.
- No empty catch blocks or swallowed exceptions—always log or rethrow.
- No public APIs with concrete types (use interfaces like `IEnumerable`, `IDictionary`).

## Example: Minimal Engine Usage

See `README.md` for a minimal example of deploying and running a process:

```csharp
var engine = await new EngineBuilder()
    .UseInMemoryStorage()
    .BuildAsync();
// ...
```

## Key Files & Directories

- `VertexBPMN.Core/` – Core engine logic, services
- `VertexBPMN.Api/` – Core engine logic, services, and interfaces
- `VertexBPMN.Application/` – Core engine logic, services, and interfaces
- `VertexBPMN.Domain/` – Core engine logic, services, and interfaces
- `VertexBPMN.Infrastructure/` – Core engine logic, services, and interfaces
- `VertexBPMN.Integration/` – Core engine logic, services, and interfaces
- `VertexBPMN.Plugins/` – Core engine logic, services, and interfaces
- `VertexBPMN.Studio/` – Core engine logic, services, and interfaces
- `README.md` – Project overview, usage, and architecture
- `.github/` – Contribution, issue, and PR templates


Always follow docs/AI-CODEGEN-GUIDE.md.
NEVER modify existing Camunda-compatible routes/DTOs; only add new APIs under /cmmn, /events, /migrations, /history (cleanup), /connectors.
Enforce tenant-aware queries (X-Tenant-Id), OpenAPI-first, TDD (contracts before code), and additive DB migrations only.
If unsure, ask to read specs/*/ (plan/spec/contracts/quickstart).

## Strict Roundtrip Roadmap
- See docs/ROUNDTRIP_STRICT_PLAN.md

---
**For any unclear or missing conventions, consult the README and CONTRIBUTING.md, or ask for clarification.**

<!-- mermaid-ai-skills:start -->
## Mermaid Diagrams

When the user asks to create, edit, or visualize a diagram, follow the
instructions in `.github/instructions/mermaid.instructions.md`.
<!-- mermaid-ai-skills:end -->
