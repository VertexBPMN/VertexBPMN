# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
5. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, or `GEMINI.md` for Gemini CLI).
6. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
8. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
VertexBPMN™ is a next-generation process engine for the .NET ecosystem, designed for full BPMN 2.0 and DMN 1.3 conformance, API compatibility with Camunda, and seamless integration with bpmn-io toolkits. The engine will support process orchestration, decision automation, multi-tenancy, and cloud-native deployment, with a focus on performance, reliability, and developer experience. The technical approach leverages .NET 9, C# 13, and modern architectural patterns, with conformance and interoperability validated by MIWG and DMN TCK test suites.

## Technical Context
**Language/Version**: C# 13, .NET 9
**Primary Dependencies**: Entity Framework Core, Dapper, OpenTelemetry, Prometheus, bpmn-io (integration), DMN engine (FEEL subset or external), OAuth2/OpenID Connect, Docker, NuGet
**Storage**: PostgreSQL, SQL Server, SQLite (for dev/test)
**Testing**: xUnit, MIWG Test Suite, DMN TCK, property/fuzz tests, integration tests, StyleCop, Roslyn analyzers
**Target Platform**: Linux server, Docker, Kubernetes, Windows (dev)
**Project Type**: single (core engine, SDK, CLI, tests)
**Performance Goals**: High throughput (≥1000 process starts/sec), low latency (<200ms p95 for sync tasks), horizontal scalability
**Constraints**: Must pass MIWG/DMN TCK, support multi-tenancy, RBAC, OpenAPI, extensibility, plugin SPI, conformance to BPMN/DMN/CMMN specs
**Scale/Scope**: Enterprise-grade, 10k+ concurrent process instances, multi-tenant, extensible for plugins/adapters

## Constitution Check

**Simplicity**:
- Projects: 3 (core engine, SDK/CLI, tests)
- Using framework directly for DB, API, and orchestration; DTOs only for serialization differences
- Avoid unnecessary patterns; repository/UoW only if justified by testability or migration needs

**Architecture**:
- Each feature as a library (core, SDK, CLI)
- CLI exposes deployment, process start, and test commands
- Library docs planned in llms.txt format

**Testing (NON-NEGOTIABLE)**:
RED-GREEN-Refactor cycle enforced; contract/integration/E2E/unit tests in order; real DBs for integration; MIWG/DMN TCK for conformance; no implementation before failing tests

**Observability**:
Structured logging, OpenTelemetry tracing, unified logs, error context

**Versioning**:
Semantic versioning (MAJOR.MINOR.BUILD), build increments, migration plans for breaking changes

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure]
```

**Structure Decision**: Option 1 (single project: src/models, src/services, src/cli, src/lib, tests/*)

## Phase 0: Outline & Research

1. **Extract unknowns from Technical Context** above:
   - Security/data protection standards required (GDPR, ISO, others?)
   - Data retention/deletion policies (define specifics)
   - DMN engine: .NET FEEL subset or external service?
   - Integration patterns for bpmn-io, DMN, form-js
   - Best practices for multi-tenancy, RBAC, plugin SPI

2. **Generate and dispatch research agents**:
   - Task: "Research required security/data protection standards for BPMN engine in enterprise context"
   - Task: "Define data retention/deletion policies for process engine"
   - Task: "Evaluate DMN engine options: .NET FEEL subset vs. external service"
   - Task: "Find best practices for bpmn-io, DMN, form-js integration in .NET/C#"
   - Task: "Research multi-tenancy, RBAC, plugin SPI for .NET process engines"

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts

*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Define entities: Prozessmodell, Entscheidungslogik, Benutzer, Organisation, Prozessinstanz, Ereignis, plus supporting tables (job, variable, task, history_event, incident, etc.)
   - Specify fields, relationships, validation rules, state transitions

2. **Generate API contracts** from functional requirements:
   - REST endpoints for process deployment, start, instance management, task operations, job execution, history/audit, identity, etc.
   - OpenAPI schema for all endpoints, compatible with Camunda REST

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `/scripts/update-agent-context.sh copilot` for your AI assistant
   - Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, copilot-instructions.md

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- Each contract → contract test task [P]
- Each entity → model creation task [P] 
- Each user story → integration test task
- Implementation tasks to make tests pass

**Ordering Strategy**:
- TDD order: Tests before implementation 
- Dependency order: Models before services before UI
- Mark [P] for parallel execution (independent files)

**Estimated Output**: 25-30 numbered, ordered tasks in tasks.md

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |


## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [ ] Phase 0: Research complete (/plan command)
- [ ] Phase 1: Design complete (/plan command)
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [ ] Initial Constitution Check: PASS
- [ ] Post-Design Constitution Check: PASS
- [ ] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*