
I am GitHub Copilot.

Below is an incremental, zero-break migration plan to merge and supersede the Engine (runtime) BpmnParser by evolving the existing Roundtrip (Strict/Normalized) parser into a single world‑class unified parser. Goal: full strict fidelity + configurable runtime extraction + high performance + observability. Each phase is additive, guarded by options, with measurable acceptance criteria.

---

## Guiding Principles

1. Single Source of Truth: One parser core; projections (strict/raw view, normalized/runtime view).
2. Capability-Driven: Feature switches (options) + capability descriptor (IBpmnParserCapabilities).
3. Zero-Break Migration: Existing public APIs keep behavior; new features opt‑in first, then default-on after stabilization.
4. Memory Discipline: Raw capture only when needed; lazy clone; ID interning; pooling.
5. Observability-first: Structured diagnostics (non-throw) + configurable “fail-fast”.
6. Deterministic Output: Strict mode preserves namespaces, ordering, vendor blocks; runtime projection uses normalized simplifications.
7. Test-Gated: Add RED tests before code for each missing feature (strict golden + runtime adapter tests + performance threshold checks).

---

## Capability Matrix Delta (What Roundtrip Lacks vs. Engine)

| Engine Feature | Roundtrip Status | Target Integration Mode |
|----------------|------------------|-------------------------|
| OpenTelemetry Span + Attributes | Missing | Optional (ParserOptions.EnableTracing) |
| ILogger instrumentation | Missing | Optional (ParserOptions.Logger) |
| Collaboration parsing (participants, messageFlow) | Partial (not integrated) | Full raw + normalized |
| Vendor extension “flattened” attribute shapes (camunda, zeebe, flowable, etc.) | Not normalized (only raw & flattened dictionaries per element) | Dual: Raw preserved + NormalizedProjection |
| ScriptTask specialized attribute extraction (scriptFormat, script, resultVariable) | Not extracted | Add normalized extraction layer |
| Form fields / task listeners JSON flatten recreation | Not built | Construct NormalizedProjectionExtensions map |
| Runtime JSON validation (zeebe:ioMapping, etc.) | Not implemented | Validation layer (toggle) |
| MCP service task attributes | Not extracted | Runtime projection adapter |
| Fail-fast exceptions vs. diagnostic list | Diagnostics only | Configurable: ThrowOnFatalValidation |
| DefaultFlow condition violation → exception (runtime) | Only diagnostic | Promote to severity classification |
| Participant / MessageFlow integration | Missing | Raw + normalized collections |
| Collaboration root retention | Missing | Optional raw capture |
| Execution projection (lightweight model) | Missing | IRuntimeView adapter |

---

## New Parser Options (Proposed)

```csharp
public sealed class UnifiedBpmnParserOptions : BpmnParserOptions
{
    // Observability
    public bool EnableTracing { get; init; } = false;
    public bool EnableLogging { get; init; } = false;

    // Runtime projection
    public bool BuildRuntimeProjection { get; init; } = false;
    public bool ValidateRuntimeSemantics { get; init; } = true;

    // Vendor normalization
    public bool NormalizeVendorExtensions { get; init; } = true;

    // Failure handling
    public bool ThrowOnFatalValidation { get; init; } = false;
    public ValidationSeverity MinimumThrowSeverity { get; init; } = ValidationSeverity.Error;

    // Raw capture toggles (already partly exist)
    public bool CaptureCollaborationRaw { get; init; } = true;
    public bool CaptureGlobalElementsRaw { get; init; } = true;
    public bool CaptureRawEventDefinitions { get; init; } = true;
    public bool CaptureRawExtensions { get; init; } = true;

    // Projections & performance
    public bool BuildNormalizedAttributeIndex { get; init; } = true;
    public bool BuildPriorityNamespaceIndex { get; init; } = true;

    // Memory & speed
    public bool UseIdInterning { get; init; } = true;
    public bool UseSharedStringPool { get; init; } = true;
    public bool UseElementMetadataCache { get; init; } = true;
}
```

---

## Phase Roadmap

### Phase 0 – Baseline & Safety Net
Tasks:
- Introduce IBpmnParserCapabilities (flags like SupportsStrictRoundtrip, SupportsRuntimeProjection, SupportsCollaboration).
- Add structural & behavioral regression tests for current Roundtrip parser.
Acceptance:
- All existing tests remain green.
- Capabilities exposed without functional change.

### Phase 1 – Collaboration & Global Entities Unification
Tasks:
- Parse participants, messageFlows, store raw (if option).
- Add global element index (messages/signals/errors/escalations) for fast ref validation.
- Add tests: golden collaboration models (strict) + runtime projection integrity.
Acceptance:
- Golden roundtrip unchanged for non-collab inputs.
- New tests verify participants/messageFlow appear in strict & normalized projection.

### Phase 2 – Vendor Extension Dual Handling
Tasks:
- Extract vendor-specific nodes (camunda, zeebe, flowable, cib, jbpm, osmanthus, alfresco, mcp) into:
  - RawExtensionElements (already) AND
  - NormalizedExtensionMap (key → structured object serialized to JSON) for runtime.
- Add resolver for priority attribute namespaces.
- Add tests: strict (raw unchanged) + runtime (flattened set).
Acceptance:
- Original <extensionElements> untouched in strict output.
- Projection exposes expected normalized keys; JSON structure validated.

### Phase 3 – Advanced Validation Layer
Tasks:
- Implement ValidationSeverity (Info/Warning/Error/Fatal).
- Rules to port/add:
  - DefaultFlowWithCondition (Error)
  - MultiInstance conflict (Warn)
  - Link event unmatched / multiple throws (Error)
  - Cancel / Terminate outside transaction (Warn)
  - Boundary compensation must be cancelActivity=false (Error)
  - Unknown message/signal/error/ escalation references (Error)
  - Duplicate IDs (Error)
  - SequenceFlow endpoint existence (Error)
- Add central validator pipeline.
- Optionally throw when ThrowOnFatalValidation=true.
Acceptance:
- Validation tests matrix (positive/negative).
- Config toggle proves non-throw vs throw behaviors.

### Phase 4 – Runtime Projection Model
Tasks:
- Define lightweight RuntimeProcessModel (id, tasks, events, flows, vendorNormalizedAttributes).
- Adapter: BuildRuntimeProjection=true triggers differential building (no raw clone).
- Provide mapping tests (scriptTask, userTask potentialOwner, multiInstance semantics).
Acceptance:
- Projection builds under 10% overhead (benchmark placeholder).
- Unit tests confirm extraction parity with Engine parser.

### Phase 5 – Observability Integration
Tasks:
- Introduce optional tracing (Start span: ParseBpmnStrict / ParseBpmnNormalized).
- Logger hooks at key stages: ParseStart, PhaseComplete, ValidationSummary, ProjectionBuilt.
- Ensure zero allocation when disabled (guard early).
Acceptance:
- Tracing test (mock tracer) sees expected span attributes (#nodes, #warnings).
- Logging test (in-memory sink) captures structured messages.

### Phase 6 – Performance & Memory Layer
Tasks:
- Add string interning & shared atom table (ID + common attribute names).
- Lazy clone for RawExtensionElements (only on first strict serialization).
- Pool temporary collections (ArrayPool / ValueListBuilder where feasible).
- Add microbenchmark project: N models (small, medium, large).
Acceptance:
- Overhead Strict vs baseline ≤ target (e.g., +15% parse time).
- Memory reclaimed (GC.GetAllocatedBytesForCurrentThread delta) documented.

### Phase 7 – Event Definition Enrichment
Tasks:
- Normalize event definitions into strongly-typed objects (reuse existing) + maintain raw fallback.
- Add vendor / unknown event definitions raw presence check & diagnostic if mutated path chosen.
Acceptance:
- Golden event models byte-identical.
- Projection sees typed definitions count consistent.

### Phase 8 – Namespace & Serialization Harmonization
Tasks:
- Add deterministic normalization serializer (NormalizedProjectionSerializer) for runtime deployment artifacts.
- Provide canonical sort (optional) per config.
- Provide hash-of-structural-model for cache invalidation.
Acceptance:
- Structural equality tests: same logical model → stable hash.
- Strict serializer no regressions.

### Phase 9 – Engine Parser Shadow Mode
Tasks:
- Wrap legacy Engine parser behind facade that delegates to unified parser projection.
- Diff engine vs unified output for representative fixtures (mismatch diagnostics).
- Deprecation warnings (internal) when Engine parser directly used.
Acceptance:
- All runtime tests pass using unified parser.
- Legacy parser removed from default DI registration path (warn only).

### Phase 10 – Full Replacement & Cleanup
Tasks:
- Remove duplicate vendor extraction logic from Engine parser.
- Update documentation (README, architecture diagrams).
- Mark old Engine.BpmnParser [Obsolete] with scheduled removal version.
Acceptance:
- CI green; consumers build only with unified parser.
- Release notes & migration guide published.

### Phase 11 – Hardening
Tasks:
- Fuzz tests (malformed XML fragments).
- Stress test (high concurrency parse).
- Security review (XXE prevention already by default XDocument parse config—verify).
- Snapshot large model memory profiling.
Acceptance:
- No regressions under 10k parallel parses simulation.
- Fuzz harness executes N random perturbations without crash.

### Phase 12 – Extended Ecosystem Features (Optional)
Tasks:
- Pluggable vendor handlers (e.g., IBpmnVendorExtensionInterpreter).
- Partial streaming parse mode (SAX hybrid) for extremely large BPMN.
- Policy-based redaction (strip confidential extensions on projection).
Acceptance:
- Extension handler test injecting new namespace logic passes.
- Stream mode memory footprint improved on large file > 5MB.

---

## Test Strategy (Layered)

| Layer | Focus | Tooling |
|-------|-------|---------|
| Golden Roundtrip | Byte or canonical equality | Existing Strict tests |
| Structural | Element/attribute sets & order | StructuralCompare helper |
| Projection | Runtime model parity vs Engine | Adapter comparison tests |
| Validation | Rule matrix (table-driven) | Parametrized xUnit |
| Performance | Time & allocation budgets | BenchmarkDotNet / custom harness |
| Resilience | Malformed input, partial XML | Fuzz & mutation tests |
| Observability | Logger + Tracer events | In-memory sink + mock tracer |

---

## Risk Table

| Risk | Impact | Mitigation |
|------|--------|------------|
| Feature Creep stalls unification | Delays rollout | Lock Phase scope; defer optional ideas |
| Memory spikes due to raw + projection duplication | High | Lazy clone, conditional capture |
| Validation false positives | Friction for users | Severity levels + configurable toggles |
| Logging overhead when disabled | Performance | Early option guard, no closures |
| Hash mismatches due to nondeterministic ordering | Caching inefficiency | Canonical ordering & deterministic serialization pass |

---

## KPI Targets

| KPI | Target |
|-----|--------|
| Strict parse overhead vs current Roundtrip | ≤ +10% |
| Runtime projection parse time vs legacy Engine parser | ≤ +15% |
| Memory increase (Strict mode with all features ON) | ≤ +25% |
| Golden models exact / canonical pass rate | 100% |
| Validation false positive rate (seed corpus) | < 1% |
| Benchmark throughput (small model) | ≥ 90% legacy Engine parser |

---

## Minimal Initial PR (Phase 1 Skeleton)

1. Introduce unified options + capabilities interface.
2. Add participants + message flows parsing to Roundtrip (behind option).
3. Add simple runtime projection stub (disabled by default).
4. Add tests: Parsing with & without option → identical strict output when disabled.

---

## Migration Strategy (Adoption Timeline)

| Sprint | Deliverables |
|--------|--------------|
| 1 | Phase 0–1 (Collaboration) |
| 2 | Phase 2–3 (Vendor dual + validations) |
| 3 | Phase 4–5 (Runtime projection + observability) |
| 4 | Phase 6–7 (Perf + event enrichment) |
| 5 | Phase 8–9 (Serializer harmonization + shadow mode) |
| 6 | Phase 10–11 (Replacement + hardening) |

---

Below is a prioritized, structured set of validation rules for the unified BpmnParser. Focus first on rules with highest defect-prevention impact and lowest implementation complexity. Each rule includes: Purpose, Trigger, Default Severity (F = Fatal/throws if configured, E = Error, W = Warning, I = Info), and Notes.

---

## Priority Tier 1 (Immediate – Core Structural & Referential Integrity)

| Rule | Purpose | Trigger | Severity | Notes |
|------|---------|---------|----------|-------|
| MissingProcess | Ensure root process exists | No <process> | F | Already partly present; unify diagnostic format |
| DuplicateId | Prevent graph corruption | Two elements share same id | E/F | Maintain hash set during parse |
| MissingIdOnFlowNode | Guard ambiguous references | Flow node w/out id | E | Already diagnostic; unify severity |
| SequenceFlowEndpointExistence | Ensure valid topology | sourceRef or targetRef not found among flow nodes | E | Build set while parsing |
| DefaultFlowWithCondition | BPMN spec: default cannot have condition | sequenceFlow marked default & has conditionExpression | E | Already in strict path; centralize |
| BoundaryAttachedRefInvalid | Runtime correctness | boundaryEvent attachedToRef not pointing to existing activity/subprocess | E | Add specific diagnostic code |
| ReferencedMessageMissing / SignalMissing / ErrorMissing / EscalationMissing | Event reference integrity | EventDef references unknown global element | E | Use global element index |
| LaneFlowNodeRefInvalid | Model consistency | lane.flowNodeRef points to non-existent node | W | Keep laneSet fidelity |
| DataObjectReferenceTargetMissing | Data linkage integrity | dataObjectReference.dataObjectRef missing | E | Quick win |
| AssociationEndpointMissing | Artifact coherence | association sourceRef/targetRef invalid | W/E | End-user impact moderate |

---

## Priority Tier 2 (Semantic BPMN Constraints)

| Rule | Purpose | Trigger | Severity | Notes |
|------|---------|---------|----------|-------|
| MultiInstanceConflict | Clarify loop semantics | loopCardinality + collection-like attribute together | W | Provide suggestion to remove one |
| CancelEndOutsideTransaction | Spec semantics | cancelEventDefinition in non-transaction subprocess/process | W | Already in strict; unify |
| TerminateEndOutsideTransaction (optional) | Over-modeling hint | terminate in non-transaction (if policy) | W (configurable) | Make togglable |
| BoundaryCompensationMustBeNonInterrupting | Correct compensation semantics | boundary compensate event without cancelActivity=false | E | Already partially there |
| LinkEventUnmatched | Prevent dead link references | Throw link not matched by catch (or catch unmatched) | E | Track names & roles |
| LinkEventMultipleThrowers | Disallowed multiplicity | More than one throw with same link name | E | Simple counter |
| EventBasedGatewayNonCatchEvent | Spec rule | event-based gateway outgoing target not a catch event | E | Need gateway -> outgoing resolution |
| EventSubprocessStartEventTypeInvalid | Spec rule | event subprocess startEvent not of allowed types | E | Allowed: message, timer, signal, escalation, conditional, error, compensation? (Check BPMN 2.0) |
| MultipleStartEventsExecutable (config) | Execution entrypoint clarity | >1 start event in executable process (if policy) | W | Configurable best practice |
| OrphanedEndEvent (optional) | Detect dead branches | End event unreachable from any start | I/W | Graph traversal |
| UnreachableFlowNode | Model quality | Node not reachable from any start (except artifact) | I/W | BFS from starts |
| DeadSequenceFlow | Not traversable in any path | Flow unreachable given graph connectivity | I | Keep cheap adjacency map |

---

## Priority Tier 3 (Advanced Runtime & Vendor Consistency)

| Rule | Purpose | Trigger | Severity | Notes |
|------|---------|---------|----------|-------|
| ZeebeTaskDefinitionMissingType | Execution contract | zeebe:taskDefinition element missing type | E | Vendor-specific toggle |
| ZeebeIoMappingMalformed | Data mapping integrity | ioMapping entries missing source/target | E | Parse structure first |
| CamundaFormFieldMalformed | UI form fidelity | formField missing id/type | W/E | Configurable |
| CamundaAssigneeEmpty | Useless assignment | camunda:assignee empty string | W | Low severity |
| FlowableTaskListenerInvalid | Execution hook correctness | taskListener missing event/class/expression | W/E | Validate set membership |
| MCPServiceTaskMissingCoreAttributes | Integration reliability | mcpServiceTask without mcpServerUrl or mcpMethod | E | Already in runtime parser |
| OsmAdvancePairIncomplete | Semantics integrity | Only one of advanceType/advanceTarget | E | Already pattern in runtime |
| OsmTimeoutPairIncomplete | Timeout config validity | Only one of timeoutDuration/timeoutAction | E | Already pattern in runtime |
| JsonExtensionInvalid | Robustness | Attribute looks JSON but fails parse | W | Heuristic guard |
| PriorityNamespaceAmbiguous | Mixed priority namespaces on flows | Different namespaces for priority usage | W | Suggest normalization |
| UnsupportedEventDefinitionVendor | Visibility | Unknown *EventDefinition vendor node mutated (if mutation path) | I | Only when altering raw |

---

## Priority Tier 4 (Model Hygiene & Best Practices – Optional)

| Rule | Purpose | Trigger | Severity | Notes |
|------|---------|---------|----------|-------|
| TaskWithoutName (policy) | Readability | Task has empty name | I/W | Skip if auto-generated names present |
| GatewayWithoutDivergeConvergePair | Structural clarity | Parallel/exclusive gateway appears only once in role | I | Graph heuristic |
| ExcessiveOutgoingOnExclusiveGateway | Maintainability | Exclusive gateway with > X outgoing (config threshold) | I | Governance |
| HugeProcessNodeCount | Performance advisory | Node count exceeds threshold | I | Provide threshold metrics |
| MissingDocumentationOnRoot (policy) | Governance | No documentation on process | I | Org-specific |

---

## Cross-Cutting Validation Infrastructure

| Component | Description | Notes |
|-----------|-------------|-------|
| ValidationSeverity enum | Info, Warning, Error, Fatal | Fatal escalated from Error based on config |
| ValidationRuleDescriptor | Id, Category, DefaultSeverity, MessageTemplate, AppliesTo | Enable rule registry |
| ValidationContext | Holds model indices (byId, incoming/outgoing, refs) | Built once |
| Rule Pipelines | Structural → Referential → Semantic → Vendor → Advisory | Early exit if Fatal & ThrowOnFatalValidation |
| Configurable Filters | IncludeRules / ExcludeRules / MinSeverity | Allow consumer shaping |
| Diagnostic Format | CODE:message (e.g., VAL-SEQ-ENDPOINT:SequenceFlow sf1 targetRef t99 missing) | Machine parsable |

---

## Suggested Rule Codes

Category prefixes:
- STR- (Structural)
- REF- (Referential)
- SEM- (Semantic BPMN)
- VEN- (Vendor/Vendor-specific)
- ADV- (Advisory)

Examples:
- STR-DUP-ID
- REF-SEQUENCE-ENDPOINT
- SEM-DEFAULT-WITH-CONDITION
- SEM-LINK-UNMATCHED
- SEM-CANCEL-OUTSIDE-TX
- VEN-ZEEBE-TASKDEF-MISSING
- VEN-MCP-MISSING-ATTR
- ADV-UNREACHABLE-NODE

---

## Dependency Graph (Implementation Order)

1. Core indices: byId, type groups, adjacency (needed for >60% rules)
2. Default/condition rule (needs gateway default marking)
3. Link event map (names + role classification)
4. Multi-instance loop capture (already there) → conflict rule
5. Transaction ancestry map (list of subprocess parents + flags)
6. Vendor structured extraction (Phase 2) → vendor rules
7. Reachability graph (BFS) for unreachable/dead rules
8. Advisory metrics (counts, thresholds)

---

## Minimal Core Set (First Pass “Golden 10”)

1. STR-DUP-ID
2. REF-SEQUENCE-ENDPOINT
3. SEM-DEFAULT-WITH-CONDITION
4. REF-BOUNDARY-ATTACHED-MISSING
5. SEM-MI-CONFLICT
6. REF-GLOBAL-MESSAGE-MISSING (and similar for signal/error/escalation)
7. SEM-LINK-UNMATCHED
8. SEM-LINK-MULTIPLE-THROW
9. SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY
10. REF-LANE-FLOWNODE-MISSING

(Engine parity + immediate model safety)

---

## Configuration Mapping

| Option | Affects | Default |
|--------|---------|---------|
| ValidateRuntimeSemantics | Enables Priority Tier 1–3 | true |
| MinimumThrowSeverity | Escalates >= severity to exception | Error |
| EnableAdvancedValidation | Adds Tier 3 & 4 | true |
| IncludeRules / ExcludeRules | Fine-grained enable/disable | null |
| TreatWarningsAsErrors | Severity upgrade | false |
| VendorValidationMode (None | Basic | Strict) | Scope of vendor rules | Basic |

---

## Diagnostic Message Style

Template: CODE: Concise human message | context(id=..., extra=...)

Example:
SEM-DEFAULT-WITH-CONDITION: Default flow f3 on gateway gw1 MUST NOT have a condition | gateway=gw1
REF-SEQUENCE-ENDPOINT: SequenceFlow sf7 targetRef task99 not found | source=task1
SEM-MI-CONFLICT: Multi-instance activity userTask1 has loopCardinality AND collection; remove one | activity=userTask1

---

## Implementation Hints

- Build indices in O(n) single pass, reuse for all rules.
- Separate “collector” (build metadata) from “validator” (pure functions).
- Provide extension point: ICustomValidationRule.
- Cache rule descriptors static (no per-parse allocations).
- Use Span<char> or pooled StringBuilder only if perf metrics justify (later phases).
- Avoid throwing inside rules; aggregate then escalate at coordinator.

---

## Next Steps (Actionable)

1. Approve “Golden 10” core set.
2. Generate ValidationRuleDescriptor scaffolding + registry.
3. Implement indices & context.
4. Add RED tests for each rule (one failing fixture each).
5. Implement and flip tests to GREEN.
6. Add projection-level acceptance test ensuring no performance regression > threshold.

######################################################

feature matrix comparing the two BpmnParser implementations:

Legend: [x] implemented, [ ] missing / not applicable, (~) partial, (*) via fallback, (RT) Strict-mode only

### Summary
- Parsing/roundtrip parser (VertexBPMN.Parsing.BpmnParser) focuses on lossless XML preservation (Strict mode) + normalization mode.
- Engine parser (VertexBPMN.Engine.Parsing.BpmnParser) focuses on executable model extraction, logging, OpenTelemetry, business/runtime validation and rich vendor extension flattening.
- They serve different production roles: editor/import/export vs. runtime execution.

### Feature Matrix

| Category | Feature | Parsing BpmnParser (Roundtrip) | Engine BpmnParser (Runtime) | Production Value |
|----------|---------|--------------------------------|-----------------------------|------------------|
| Core Model | Process ID extraction | [x] | [x] | Fundamental |
| | Tasks / Events / Gateways / SequenceFlows | [x] Full (ordered) | [x] | Execution & editing |
| | Subprocess hierarchy (nesting) | [x] with parent chain & child lists | [~] flat (only top-level subprocess elements) | Structural fidelity vs. simplicity |
| | Multi-instance loop capture | [x] Raw node + normalized characteristics | [~] Basic flags/cardinality only | MI semantics & regeneration |
| | Boundary events (attachedToRef, cancelActivity) | [x] Raw & attributes preserved | [x] Simplified flags | Execution correctness |
| | Link event validation | [x] (throw/catch consistency) | [ ] | Model integrity |
| | Transaction / compensate / terminate validations | [x] | [ ] | Compliance diagnostics |
| Global Elements | Messages / Signals / Errors / Escalations | [x] Raw capture | [ ] (not parsed separately) | Referenced event correctness |
| | Artifacts (textAnnotation, association, group) | [x] Raw emit (Strict) | [x] (annotations, associations) | Documentation & tooling |
| | Lanes & laneSet hierarchy | [x] Raw laneSet + lane order | [x] (lanes only, no laneSet) | Swimlane design fidelity |
| | Collaboration (participants / messageFlow) | [ ] (not yet integrated) | [x] | Choreography / integration views |
| Extensions | Preserve unknown extensionElements | [x] (raw clone) | [x] (flattened into attributes) | Vendor interoperability |
| | Namespace prefix order preservation | [x] (Strict) | [ ] | Source diff / deterministic serialization |
| | Vendor event definitions (unknown) | [x] Raw copy | [ ] (ignored) | Roundtrip safety |
| | Priority attribute namespace retention | [x] Per-flow namespace map | [ ] | Behavior-specific engines |
| Serialization | Strict lossless serializer | [x] (namespace order, element order, raw blocks) | [ ] (generative only) | Editing & diffing |
| | Normalized serializer fallback | [x] | [x] (single mode) | Simplicity/performance |
| | Partial dirty element support | [x] (per-element mutated attribute injection) | [ ] | Targeted edits without full rebuild |
| | Dirty full fallback (diagnostic) | [x] | [ ] | Safe degradation |
| Raw Capture | Raw multi-instance nodes | [x] | [ ] | Exact vendor constructs |
| | Raw extensionElements immutable copy | [x] (deep clone) | [ ] | Post-parse mutation safety |
| | Raw documentation blocks | [x] | [ ] | Authoring / tooling |
| | Raw DI root (BPMNDiagram) | [x] (optional) | [ ] | Visual editors |
| Validation / Diagnostics | Duplicate ID detection | [x] (Strict) | [x] via exceptions | Model correctness |
| | Missing id diagnostics | [x] | [x] (throws) | Early failure |
| | Default flow with condition check | [x] | [ ] | BPMN spec alignment |
| | Cancel / terminate event context validation | [x] | [ ] | BPMN semantics |
| | Multi-instance conflict (cardinality vs. collection) | [x] | [ ] | Semantic clarity |
| | JSON structure validation for extension payloads | [ ] | [x] (tool-specific) | Runtime robustness |
| Performance | LRU cache (hash-based) | [x] Configurable | [ ] (simple doc cache keyed by full XML) | Throughput / memory control |
| | ID interning | [x] (configurable) | [ ] | Memory reduction for large graphs |
| | Optimize empty raw structures | [x] | [ ] | Heap pressure reduction |
| Observability | OpenTelemetry spans | [ ] | [x] | Production tracing |
| | ILogger integration | [ ] | [x] | Operational insight |
| Mutation API | MarkDirty / ApplyAttributeChange | [x] | [ ] | Controlled edits |
| | Partial attribute change (no global dirty) | [x] | [ ] | UI incremental editing |
| Safety / Errors | Diagnostics list (non-throw) | [x] | [~] (throws custom exceptions) | Flexible handling vs. fail-fast |
| | Exception-based strict validation | Optional | [x] | Runtime safety |
| Coverage | Event definitions typed (timer, message, signal, etc.) | [x] (rich objects) | [~] Reduced (string flag type) | Advanced logic use |
| | Loop characteristics normalized model | [x] | [ ] | MI analysis |
| | Comprehensive extension namespace harvesting | [x] (Strict) | [x] (flatten/value extraction) | Ecosystem compatibility |

### Production Advantages Comparison

| Aspect | Roundtrip Parser Strength | Engine Parser Strength |
|--------|---------------------------|------------------------|
| Model Fidelity | Byte-level reproducibility (Strict) | Simplified, lean runtime model |
| Vendor Interop | Raw preservation (no data loss) | Normalized attributes for execution |
| Mutation Workflow | Dirty + partial-dirty enables smart serialization | Runtime-focused (no mutation layer) |
| Performance (runtime) | More overhead in Strict (raw trees, cloning) | Lighter object graph, fewer allocations |
| Memory Control | Interning + conditional nulling of raw maps | Simple footprint, no raw retention |
| Observability | (Can be extended; currently minimal) | Built-in OpenTelemetry + logging |
| Failure Handling | Non-disruptive diagnostics list | Fail-fast with domain exceptions |
| Extensibility | Foundation for advanced editors & diff tools | Ready for execution pipeline integration |
| Tooling (DI / lanes / docs) | Full structural retention | Minimal execution-relevant data |
| Compliance Edge Cases | Comprehensive semantic diagnostics | Core BPMN only |

### When to Use Which (Production Guidance)

- Use Roundtrip (Strict) parser for:
  - BPMN editors, roundtrip import/export, diffing, auditing.
  - Vendor-heavy models (Camunda/Zeebe/etc.) where preservation matters.
  - Post-processing pipelines that must not alter original formatting or structure.

- Use Engine parser for:
  - Workflow execution runtime (deployment → execution).
  - High-throughput parsing where semantic richness of raw XML is not required.
  - Environments needing logging, tracing, structured validation, JSON extension interpretation.

### Potential Unification / Next Steps

| Opportunity | Action |
|-------------|--------|
| Shared core AST | Introduce neutral internal model both parsers populate at different fidelity levels |
| Pluggable validation | Export current strict diagnostics as strategy usable in engine parser |
| Observability parity | Add optional ILogger/OpenTelemetry hooks to roundtrip parser guarded by options |
| Hybrid mode | “Semi-Strict”: capture only selected raw blocks (extensions + eventDefinitions) |
| Execution enrichment | Engine parser could reuse strict parser in normalized mode when lossless not needed |

### Risks & Mitigations

| Risk | Parser Impact | Mitigation |
|------|---------------|-----------|
| Memory growth with large diagrams | Roundtrip (raw XElements) | OptimizeStrictMemory + lazy cloning |
| Hidden performance regressions | Both | Add benchmark suite (Strict vs Normalized vs Engine) |
| Divergent feature evolution | Both | Define shared interface contract + capability flags |
| Vendor extension misinterpretation | Engine | Add allowlist + fallback to raw retention (optional) |
| Lossy runtime transformations | Engine | Optionally persist original XML alongside normalized graph |

### Recommendations

1. Keep both parsers; they are complementary.
2. Expose a capability descriptor (e.g., IBpmnParserCapabilities) for feature-driven behavior.
3. Add performance benchmarks before expanding Strict usage in runtime contexts.
4. Add a thin adapter to transform Strict model → Engine model for deployments needing both fidelity and execution.
5. Optionally add logging & tracing hooks to the roundtrip parser guarded by a flag (no-op by default).

If you want I can produce:
- A code stub for a shared capability interface.
- A benchmark harness skeleton.
- An adapter mapping Strict BpmnModel to Engine BpmnModel.

Let me know which you prefer next.
#####################################################
Ich heiße GitHub Copilot.

Unten eine priorisierte Übersicht, welche Teile der VertexBPMN.Parsing.BpmnParser (Roundtrip/Strict) sinnvoll (und wie) in die VertexBPMN.Engine.Parsing.BpmnParser übernommen werden können – mit Nutzen, Aufwand, Risiken und Empfehlungen.

## 1. Hoher Nutzen / Geringes Risiko (Quick Wins)

| Feature aus Roundtrip | Status Engine | Nutzen im Runtime Parser | Empfehlung |
|-----------------------|--------------|--------------------------|------------|
| Duplicate-ID Erkennung (nicht-throw, Diagnostic) | Fehlend (nur indirekt) | Frühzeitige Modellqualität ohne sofort Exception | Warn/Diagnostic ergänzen vor Hard-Fail (Option) |
| Default-Flow-mit-Condition Check | Fehlend | Logikfehler früh erkennen | Validierung hinzufügen (StrictValidation Flag wiederverwenden) |
| BoundaryEvent attachedToRef Existenzprüfung (Roundtrip macht plus mehr) | Teilweise (Exception) | Präzisere Fehlermeldung | Differenzierte Message + Kategorie |
| Multi-Instance Konflikt (collection vs. cardinality) | Fehlend | Verhindert semantische Fehlmodellierung | Validation hinzufügen |
| Link Event Konsistenz (Unmatched / Multiple Throw) | Fehlend | Verhindert tote Pfade | Validation hinzufügen |
| Cancel / Terminate EndEvent Kontextprüfung (Transaktionserwartung) | Fehlend | Geschäftslogik-Konsistenz | Optional als Warning (nicht sofort Exception) |
| Priority-Namespace Erhaltung | Fehlend | Falls Engine später Priorität interpretiert | Beim Parsen Namespace-Kennung merken (Attribut-Metadaten) |
| Parsing von Documentation-Blöcken | Fehlend | UI/Monitoring/Generate Help | Als optionale Sammlung (List<string>) aufnehmen |
| ID Interning | Fehlend | Reduziert Memory bei großen Modellen | Opt-in Flag (ParserOptions.InternIds) portieren |
| NamespacePrefix-Erfassung (Reihenfolge) | Fehlend | Für konsistenten Re-Export / Diff | Nur falls Engine auch exportiert (Low Effort) |

## 2. Mittlerer Nutzen / Moderates Risiko

| Feature | Nutzen | Risiko | Empfehlung |
|---------|--------|-------|------------|
| RawExtensionElements (immutable Snapshot) | Auditing, Re-Export, Debugging | Mehr Speicher (potenziell groß) | Optional über ParserOptions.CaptureRawExtensions |
| RawEventDefinitions (Vendor-Erhalt) | Interoperabilität mit Spezial-Engines | Speicher & Komplexität | Nur wenn Vendor-spezifische Trigger relevant |
| RawMultiInstance XML | Vendor-Schalter (Camunda/Zeebe) exakt | Speicher | Optional: nur speichern, wenn MI-Knoten vorhanden |
| RawLanes (laneSet + lane Struktur) | Swimlane-basierte Berechtigungslogik | XML-Strukturpflege | Nur falls spätere Policy-Features geplant |
| RawGlobalElements (message/signal/error/escalation) | Schnelle lookups bei Event-Korrelation | Gering | Über kleine Map (Id→Typ) statt XElement-Liste (Runtime-optimiert) |

## 3. Geringer Nutzen / Hoher Aufwand (Eher Editor-/Roundtrip-spezifisch)

| Feature | Warum gering für Runtime | Empfehlung |
|---------|--------------------------|------------|
| Vollständiger Strict Namespace Präfix Order Replay | Nur für Byte-identische Roundtrips relevant | Nicht übernehmen |
| Partielle Dirty-Verfolgung | Editor-Funktion (UI incremental) | Auslassen |
| DI Raw (BPMNDiagram) | Execution uninteressant | Auslassen oder separater Visualizer-Service |
| Vollständiges Attribut-Ordering / OriginalName | Nur für Diff/Export | Weglassen |
| Komplett-Lossless Fallback-Mechanismen (RT-Fallback Diagnostics) | Runtime möchte fail-fast | Nicht übernehmen |

## 4. Validierungserweiterungen (detaillierte Vorschläge)

| Validator | Umsetzungsskizze | Fail vs Warn |
|-----------|------------------|-------------|
| DefaultFlowHatCondition | Während SequenceFlow-Schleife: falls flow.IsDefault && (Condition vorhanden) → CollectError | Exception (modellfehler) |
| MultiInstanceKonflikt | Beim MI-Parsing: wenn cardinality und collection -> ConflictFlag | Warn (auto-behebbar) |
| LinkEvents | Map throwName→count, catchName-Set; am Ende: count>1 oder missing catch → Fehler | Warn oder Fail konfigurierbar |
| CancelEvent außerhalb Transaction | ascend Subprocess-Kette → wenn kein Transaction | Warn |
| TerminateEvent außerhalb Transaction (optional) | analog Cancel | Warn |
| Boundary Compensation ohne cancelActivity=false | Prüfen und ggf. Fehler | Fail (Semantik) |

## 5. Technische Integrationsstrategie

| Schritt | Beschreibung | Aufwand |
|---------|--------------|---------|
| A: ParserOptions erweitern | Flags: CaptureRawExtensions, CaptureRawEvents, InternIds, EnableAdvancedValidation | Klein |
| B: Utility Layer | Neues Internal ModelValidationContext, sammelt Warnings/Errors | Mittel |
| C: Incremental Adoption | Schrittweise Aktivierung über Options statt harte Umschaltung | Klein |
| D: Memory Mitigation | Für Raw-Knoten: Nur XElement.DeepClone wenn Option aktiv | Klein |
| E: Benchmarks | Vor/Nach: Parse 1k Modelle (StrictFeatures off vs on) | Mittel |

## 6. Empfohlene Reihenfolge (Roadmap Light)

1. Quick Validation Set (Duplicate IDs, DefaultFlowCondition, LinkEvents)  
2. Multi-Instance & Boundary Semantik Checks  
3. Optional ID Interning  
4.Imports: GlobalElements index (MessageRef/SignalRef Validierung verbessern)  
5. Optional RawExtensionElements (nur wenn Auditing/Export Anforderung)  
6. NamespacePrefix + RawGlobalElements für Exportpfad (falls benötigt)

## 7. Beispiel API-Erweiterung (nur Konzept, kein Code eingebaut)

```csharp
public sealed class EngineParserOptions
{
    public bool EnableAdvancedValidation { get; init; } = true;
    public bool CaptureRawExtensions { get; init; } = false;
    public bool CaptureRawEventDefinitions { get; init; } = false;
    public bool InternIds { get; init; } = true;
}
```

Model-Erweiterung (leichtgewichtige statt komplette Raw-Übernahme):

```csharp
public sealed record RawFragmentIndex(
    IReadOnlyDictionary<string, string>? PriorityNamespaceBySequenceFlow,
    IReadOnlyDictionary<string, string[]>? LaneMemberships,
    IReadOnlyDictionary<string, string>? MultiInstanceLoopType // id -> \"standard\" | \"parallel\" | \"sequential\"
);
```

## 8. Risiken & Mitigation

| Risiko | Beschreibung | Mitigation |
|--------|--------------|-----------|
| Speicheranstieg | Raw XElements → großer Heap | Opt-In Flags + Null bei leer |
| Performanceeinbruch | Mehr Traversals & Validierungen | Early short-circuit wenn Optionen aus |
| Drift zweier Parser | Unterschiedliche Logikpfade | Gemeinsame Validator-Klasse extrahieren |
| Fehlinterpretation Vendor Extensions | Unvollständige Coverage | Unbekannte Namespaces nur speichern, nicht interpretieren |

## 9. Was Nicht Übernommen Werden Sollte

- Vollständige Deep-Roundtrip Fähigkeit (Strict) → Editor-Domäne
- Partielle Dirty-Mechanik
- DI-Baumspeicherung
- Byte-genaue Namespace/Attribut-Reproduktion

## 10. Zusammenfassung (Kurzform)

- Sofort übernehmen: erweiterte Validierungen + Duplicate/Link/MultiInstance Checks.
- Optional & wertvoll: ID Interning, PriorityNamespace Tracking.
- Nur wenn Export/Audit gefordert: RawExtensions / RawEventDefinitions.
- Alles Strict-Roundtrip Spezifische (Namespace-Order, DI, Partial Dirty) im Engine-Kontext weglassen.

Sag Bescheid, wenn du:
- Ein konkretes PR-Skelett für Schritt 1 möchtest
- Benchmark Grundgerüst brauchst
- Validierungsmodul entworfen haben willst