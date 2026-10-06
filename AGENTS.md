# VertexBPMN — instructions for coding agents

These repository instructions apply to Hermes/DeepSeek and other coding agents.
Follow the user's current task; the Azure plan below is context, not permission to
start implementation, provision resources, or publish changes automatically.

## Project and entry points

VertexBPMN is a .NET process-automation platform with a persistent BPMN runtime,
DMN/CMMN capabilities, a Blazor Studio, APIs, SDK, CLI and external workers.
Read `README.md` and the relevant implementation before making support claims.
`docs/reference/product-support-matrix.md` records support scope; verify its
claims against code and tests. A plan or green unit test is not production proof.

| Location | Responsibility |
| --- | --- |
| `src/VertexBPMN.Domain` | Domain entities and interfaces |
| `src/VertexBPMN.Application` | Application services and contracts |
| `src/VertexBPMN.Engine` | Process execution, parsing and runtime behavior |
| `src/VertexBPMN.Infrastructure` | EF persistence, messaging and integrations |
| `src/VertexBPMN.Api` | HTTP API, authentication and migration entry point |
| `src/VertexBPMN.Studio` | Blazor Interactive Server UI and editor integration |
| `src/VertexBPMN.AppHost` | Aspire local orchestration and external-service profiles |
| `src/VertexBPMN.AgentWorker` | HTTP external-task worker; not a Service Bus consumer |
| `src/VertexBPMN.Sdk`, `src/VertexBPMN.Cli` | Public client SDK and CLI |
| `tests/VertexBPMN.Tests` | Main unit/integration/acceptance suite |
| `tests/VertexBPMN.Studio.UiTests` | Local browser acceptance tests |
| `deploy/`, `docs/runbooks/`, `docs/reviews/` | Deployment assets, operations and plans |

## Working rules

- Inspect `git status`, current branch and relevant diffs first. Preserve work
  from the user and other agents. Prefer an isolated feature branch/worktree for
  implementation; use a user-specified branch when provided. Do not reset or
  delete unrelated changes. Commit/push/deploy only within user authorization.
- Read the selected plan completely, then implement dependency-ordered work
  packages. Verify actual files and APIs; do not invent configuration switches.
- Fix root causes. Do not weaken BPMN/DMN conformance assertions, rewrite valid
  fixtures to hide failures, skip failing cases, or replace execution tests with
  parser checks merely to obtain green tests.
- Keep tenant authorization, durable state, cancellation and failure handling
  intact. Never log secrets, tokens, connection strings or sensitive payloads.
- Preserve existing local and self-hosted profiles. WSLC/external services are
  valid local choices; do not require Docker or Azure credentials by default.
- Generated browser bundles must correspond to their source/build pipeline;
  inspect the Studio project targets before regenerating them.
- Communicate in the user's language (normally German). Report changed files,
  actual test results, remaining issues and the next concrete step. Distinguish
  implemented, locally tested and externally accepted. Missing infrastructure
  means an acceptance check remains unperformed, not passed.

## Build and test

Read `global.json` for the SDK and runner; currently .NET 10 with
Microsoft.Testing.Platform and xUnit v3. Use PowerShell-compatible commands on
Windows and Bash-compatible commands on Ubuntu, with repository-relative paths.
Detect the actual host; Hermes may run on an Ubuntu Azure VM, where Windows paths
and `wslc.exe` are unavailable. Preserve WSLC support in the product regardless.
Do not assume VSTest options. An available VM Managed Identity does not authorize
cloud deployment; explicitly select isolated resources for authorized cloud tests.

```text
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

The asset-skip flag is appropriate only when editor assets are unchanged or have
been separately rebuilt. `--no-build` tests require a current successful build.
For targeted test filters inspect the runner's `--help` and existing scripts.

Use `.github/workflows/ci.yml`, step **Test (CI-safe suite)**, as the authoritative
command and exclusion list for the fast suite. Do not run the unfiltered entire
suite as if it were CI-safe. External PostgreSQL/RabbitMQ, browser, outage,
recovery, conformance, load/soak, Ollama and benchmark tests have separate
prerequisites. Preserve their separation from the fast GitHub workflow.

Add regression tests for changed behavior, run the relevant local checks, then
the applicable CI-safe suite. Azure integration tests must be explicit opt-in
with isolated resources and documented cleanup. Never put cloud provisioning or
local-service requirements into ordinary PR tests.

## Azure / Service Bus assignment

For Azure work, first read:

1. `docs/reviews/2026-09-17_Azure-Deployment-und-Service-Bus-Umsetzungsplan.md`
2. `docs/runbooks/production-deployment.md`
3. Relevant current code and test files named by the selected phase.

The plan is the task source of truth; this file is a concise orientation. Verify
its implementation status on each new session. Key constraints:

- Production `IMessageDispatcher` resolves to `PersistentMessageDispatcher`,
  which writes into the database outbox. Add Service Bus below that boundary via
  `IRuntimeOutboxTransport`; do not bypass durability with a direct dispatcher.
- Preserve RabbitMQ/Kafka and local provider selection. Azure is additional.
- Inbox recovery must handle incomplete claims, handler failure, duplicate
  delivery and commit-before-ack crashes. Broker duplicate detection does not
  guarantee exactly-once business effects. Clone parsed JSON payloads that must
  outlive their document. Validate tenants and stable business idempotency keys.
- Registry provider changes include the CLI `DependencyConfigurationLoader`.
  Keep SQLite locally; Azure replicas require shared durable state.
- Shared Data Protection keys do not make the in-process OIDC token store or
  Blazor circuits distributed. Follow the session/refresh and rollout work in P4.
- Migration execution already has an API `--migrate-only` path. Verify bootstrap,
  permissions and its startup dependencies rather than adding a competing path.
- A revision with zero HTTP traffic can still run background consumers/jobs.
  Account for that during staged rollout and broker cutover.
- Azure resource creation requires the user's deployment authorization and
  concrete subscription/budget parameters; prepare reviewable code first.

After an authorized package is complete, update its plan status with files,
commands/results and outstanding external checks. Do not mark an entire phase
complete when any mandatory acceptance criterion remains unverified.

# Codex repository guidance

## Working agreements

- Before editing, inspect the existing solution/projects, README, applicable local instructions, `.editorconfig`, `global.json`, `Directory.Build.props` and CI workflows if present. Derive SDK/language versions, architecture and build/test commands from actual files; do not assume paths or versions.
- Preserve existing architecture, public contracts and user changes. Keep diffs small and focused; no opportunistic repository-wide refactoring or new dependencies without a justified task requirement.
- Prefer meaningful domain names, cohesive methods, existing abstractions and explicit error behavior. Use async contracts and propagate cancellation for cancellable I/O. Avoid async void except genuine required event handlers.
- Choose collection return types by their semantics; preserve intentional lazy/async streams. Avoid unclear boolean mode switches. Do not remove interfaces solely because they have one implementation.
- Do not weaken authentication, authorization, token validation, tenant boundaries or tests to make a build pass. Do not implement custom OAuth/OIDC/cryptographic protocols as a shortcut.
- Do not expose secrets, rotate keys, apply database migrations, deploy, push or perform destructive Git operations unless the user specifically authorizes them.

## Clean-code workflow

- For explicit C# clean-code reviews, use `$dotnet-clean-code-review` from `.agents/skills/dotnet-clean-code-review/SKILL.md`.
- After implementing or refactoring C# code, use that skill for a final review of the files changed in this task. Do not require a repository-wide sweep. Fix confirmed issues in your own changes when within the authorized implementation scope; leave unrelated user changes intact.
- For general code/PR reviews, prioritize correctness, regressions, security and missing tests. Use the clean-code skill as a supplement, not a substitute. Review-only requests do not authorize edits.
- If skill discovery is unavailable, read the SKILL.md at the path above and follow its workflow using available tools; report this fallback.

## Verification and handoff

- For implementation tasks, discover and run the relevant existing build, test and formatting verification commands. Use check-only formatting. Start with affected projects/tests, then broaden when necessary.
- Do not install missing SDKs/tools, perform restores requiring unapproved network access, run tests against production, or bypass sandbox policy. Report blockers.
- State exactly what changed, what was checked, the actual command results and remaining risks. Never claim tests/build succeeded if they did not run.
- Product documentation is not proof of implementation. For feature audits, provide code/config/test evidence and distinguish implemented, partial, documented-only and not found within the inspected scope. An OIDC client or Data Protection keyring does not by itself establish an OIDC authority or signing-key rotation.
