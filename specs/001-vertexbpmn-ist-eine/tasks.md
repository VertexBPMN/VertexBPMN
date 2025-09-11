# Tasks: VertexBPMN™ Prozess-Engine für das .NET-Ökosystem

**Input**: Design documents from `/specs/001-vertexbpmn-ist-eine/`
**Prerequisites**: plan.md (required), research.md

## Execution Flow (main)
```
1. Load plan.md from feature directory
2. Load research.md for technical decisions and unknowns
3. Generate tasks by category:
   - Setup: project init, dependencies, linting
   - Tests: contract tests, integration tests
   - Core: models, services, CLI commands
   - Integration: DB, middleware, logging
   - Polish: unit tests, performance, docs
4. Apply task rules:
   - Different files = mark [P] for parallel
   - Same file = sequential (no [P])
   - Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness
9. Return: SUCCESS (tasks ready for execution)
```

## Tasks

### Setup
T001  Initialize project structure in `src/` and `tests/` folders
T002  Setup .NET 9 solution, add projects for core engine, SDK/CLI, and tests
T003  Add dependencies: Entity Framework Core, Dapper, OpenTelemetry, Prometheus, OAuth2/OpenID Connect, Docker support
T004  Configure linting and code style: StyleCop, Roslyn analyzers
T005  Setup CI pipeline for build, test, and code quality checks

### Research & Decisions
T006  [P] Resolve all NEEDS CLARIFICATION in research.md and document decisions
T007  [P] Document chosen security/data protection standards (GDPR, ISO, etc.) in `docs/decisions/security.md`
T008  [P] Document data retention/deletion policies in `docs/decisions/data-retention.md`
T009  [P] Document DMN engine integration decision in `docs/decisions/dmn-engine.md`
T010  [P] Document bpmn-io, DMN, form-js integration patterns in `docs/decisions/integration.md`
T011  [P] Document multi-tenancy, RBAC, plugin SPI approach in `docs/decisions/extensibility.md`

### Core Model & Architecture
T012  [P] Design and implement core entities in `src/models/` (Prozessmodell, Entscheidungslogik, Benutzer, Organisation, Prozessinstanz, Ereignis, job, variable, task, history_event, incident)
T013  [P] Define relationships, validation rules, and state transitions in `src/models/`
T014  [P] Create canonical SQL schema and migration scripts for PostgreSQL, SQL Server, SQLite in `src/models/schema/`

### API & Contracts
T015  [P] Design OpenAPI schema for REST endpoints in `specs/001-vertexbpmn-ist-eine/contracts/openapi.yaml`
T016  [P] Implement contract tests for each endpoint in `tests/contract/`
T017  Implement REST API controllers in `src/services/`
T018  Implement SDK/CLI commands for deployment, process start, and test execution in `src/cli/`

### Integration & Services
T019  Implement database integration and migrations in `src/services/`
T020  Integrate OpenTelemetry tracing and Prometheus metrics in `src/services/`
T021  Implement authentication/authorization (OAuth2/OpenID Connect, RBAC, multi-tenancy) in `src/services/`
T022  Integrate bpmn-io, DMN engine, and form-js adapters in `src/services/`
T023  Implement plugin SPI for extensibility in `src/lib/`

### Testing & Validation
T024  [P] Write integration tests for user stories and edge cases in `tests/integration/`
T025  [P] Write property/fuzz tests for BPMN/DMN models in `tests/integration/`
T026  [P] Integrate MIWG Test Suite and DMN TCK in `tests/integration/`
T027  [P] Write unit tests for core modules in `tests/unit/`
T028  [P] Write quickstart test and tutorial in `specs/001-vertexbpmn-ist-eine/quickstart.md`

### Polish & Documentation
T029  [P] Write developer guide, API docs, architecture diagrams, and runbooks in `docs/`
T030  [P] Prepare release notes, conformance test results, and performance benchmark report in `docs/`
T031  [P] Finalize OpenAPI spec and NuGet package for SDK
T032  [P] Validate all tasks, update progress tracking, and prepare for PR/merge

## Parallel Execution Guidance
- Tasks marked [P] can be executed in parallel (different files, no dependencies)
- Example: T006–T011 (research/docs), T012–T015 (models/contracts), T024–T028 (tests/docs)
- All setup tasks (T001–T005) must be completed before core implementation
- Tests (T016, T024–T028) must be written before corresponding implementation tasks

## Dependency Notes
- Setup → Research → Models → Contracts → Services → Endpoints → Integration → Tests → Polish
- Contract tests and integration tests must fail before implementation (TDD)
- Documentation and polish tasks run in parallel after core implementation

---

**Ready for execution. Each task is specific and test-first.**
