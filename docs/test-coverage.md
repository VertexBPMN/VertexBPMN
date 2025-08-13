# VertexBPMN API Integration Test Coverage


This document describes the integration and unit test coverage for the VertexBPMN API and Core Engine. All endpoints and engine features are tested using in-memory services to ensure correct wiring, serialization, business logic, and edge-case/error handling.


## Covered Endpoints & Engine Features

- **Health**: `/api/health` — Returns 200 OK if the API is running.
- **Repository**: `/api/repository` (POST, GET) — Deploys and retrieves process definitions.
- **Runtime**: Start, retrieve, and list process instances (in-memory simulation).
- **Task**: Claim, complete, delegate, and list user tasks (in-memory simulation).
- **History**: List and retrieve history events (in-memory simulation).
- **Management**: Suspend, resume, and delete process instances (in-memory simulation).
- **Identity**: Validate users, list users by group, list tenants (in-memory simulation).
- **Decision**: Evaluate DMN decisions and retrieve definitions (in-memory, with complex input/output).
- **BPMN Engine**: Full coverage for all BPMN 2.0 elements (events, gateways, subprocesses, multi-instance, event subprocesses, boundary events, business rule tasks, sequence flows, error handling).
- **DMN Engine**: DecisionService integration, complex input/output, error and null handling.


## Advanced & Edge-Case Coverage

- Nested subprocesses and event subprocesses
- Boundary events on user tasks
- Multi-instance subprocesses
- BusinessRuleTask with DMN integration
- Error handling for missing start events, invalid models, unknown tasks, and null DMN decisions
- Complex DMN input/output (objects, lists, types)


## Test Strategy

- Use `WebApplicationFactory<Program>` for in-memory API hosting.
- Use `HttpClient` for endpoint calls and assertions.
- Use xUnit for all unit and integration tests.
- Validate status codes, response payloads, engine traces, and error handling.
- Expand tests as new endpoints and features are added.

---


*This file is auto-generated and updated as part of the continuous integration plan. Last update: August 11, 2025.*
