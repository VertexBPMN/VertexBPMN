Specific, incremental changes (prioritized). Implement in phases to avoid large risky refactor.

Phase 0 – Quick correctness fixes
- Fix ValidateBpmnModel: validate flow.SourceRef / flow.TargetRef exist (currently compares flow.Id).
- Serialize conditionExpression on sequenceFlow (use CDATA).
- Capture default flow on gateways: read @default attribute and flag the matching sequenceFlow (e.g. IsDefault).
- Normalize eventDefinitionType casing (store canonical lower-case enum/string).

Phase 1 – Core model enrichment (add without breaking existing consumers)
1. Create lightweight domain types (no behavior yet):
   - abstract record EventDefinition(string Kind);
     records: TimerEventDefinition(string? TimeDate,string? TimeDuration,string? TimeCycle),
              MessageEventDefinition(string MessageRef,string? CorrelationKey),
              SignalEventDefinition(string SignalRef),
              ErrorEventDefinition(string ErrorRef,string? ErrorCode),
              EscalationEventDefinition(string EscalationRef),
              LinkEventDefinition(string Name,string? Target),
              ConditionalEventDefinition(string Condition),
              CompensationEventDefinition(string? ActivityRef),
              CancelEventDefinition(),
              TerminateEventDefinition().
2. Replace string eventDefinitionType in BpmnEvent with IReadOnlyList<EventDefinition> Definitions (keep old property temporarily [Obsolete] for compatibility; fill it from Definitions[0].Kind).
3. Introduce loop characteristic models:
   - abstract record LoopCharacteristics(string Kind);
     records: StandardLoopCharacteristics(string? LoopCondition,bool TestBefore,int? LoopMaximum),
              MultiInstanceLoopCharacteristics(bool IsSequential,int? LoopCardinality,string? Collection,string? ElementVariable,string? CompletionCondition).
   - Add LoopCharacteristics? Loop to BpmnSubprocess and (later) Tasks if needed.
4. Add properties to BpmnSequenceFlow:
   - bool IsDefault
   - string? ConditionExpression (instead of Attributes["conditionExpression"]).
5. Add callActivity support: new optional string? CalledElement on BpmnTask.

Phase 2 – Parsing pipeline refactor (maintain old API)
Split ParseAsync into internal steps:
- LoadXDocument
- ParseProcessCore (id/name)
- ParseFlowNodes (events, tasks, gateways, subprocesses)
- ParseSequenceFlows
- ParseArtifacts (lanes, dataObjects, etc.)
- ParseCollaboration
- EnrichEventDefinitions (transform raw XML children inside events into EventDefinition instances)
- EnrichLoopCharacteristics (multi-instance + standard loop)
- DetectDefaultFlows
- ValidateModel (expanded)
Keep old public method; inside call steps sequentially.

Phase 3 – Event definitions parsing
Inside ParseEvents:
- Collect child elements of each event (e.Elements()) by localName:
  - timerEventDefinition: parse timeDate/timeDuration/timeCycle children
  - messageEventDefinition: read @messageRef + any correlationKey (vendor extension)
  - signalEventDefinition: @signalRef
  - errorEventDefinition: @errorRef + maybe referenced error element (if errors section added later)
  - escalationEventDefinition: @escalationRef
  - linkEventDefinition: @name (store once; separate linkage resolution pass later)
  - conditionalEventDefinition: <conditionExpression> inner text / CDATA
  - compensateEventDefinition: @activityRef
  - cancelEventDefinition: no extra data
  - terminateEventDefinition: (end event type detection)
Push constructed EventDefinition(s) into BpmnEvent.Definitions.

Phase 4 – Multi-instance & loop parsing
- Standard loop: detect <standardLoopCharacteristics>
  - loopCondition (inner expression)
  - testBefore (attribute)
  - loopMaximum (attribute)
- Multi-instance: existing; extend to read:
  - loopCardinality (already)
  - collection (camunda:collection / zeebe:inputCollection)
  - elementVariable (camunda:elementVariable / zeebe:outputElement)
  - completionCondition (child)
- Map to LoopCharacteristics.

Phase 5 – Subprocess containment (non-breaking)
- Add optional SubprocessId to BpmnEvent/Task/Gateway/SequenceFlow for elements whose ids start with {subprocessId}_ or (preferred) whose XML ancestor is a subProcess.
- Change Parse to walk descendants of each subProcess, tagging contained nodes (without removing them from global lists yet).
- Later (Phase 7) move to hierarchical collections.

Phase 6 – Validation enhancements
Add:
- Unique ID check (HashSet).
- Each boundaryEvent.AttachedToRef targets existing Task/Subprocess.
- Compensation boundary only on transaction or compensatable activity.
- Cancel End only inside transaction subprocess.
- Default flow must exist among outgoing of the gateway and must have no condition.
- ConditionalSequenceFlow must have conditionExpression.
- Event Subprocess must have exactly one startEvent with Definitions.Count>0 (no plain none start).
- Link events: each throw link must have matching catch link (name).
- Multi-instance cardinality vs collection mutual exclusivity warning.

Phase 7 – Serialization improvements
- Serialize event definitions with proper child elements (timerEventDefinition etc.).
- Serialize conditionExpression for flows if present (CDATA).
- Write default attribute on gateway (if any IsDefault flow).
- Serialize loop characteristics (standardLoopCharacteristics or multiInstanceLoopCharacteristics).
- Preserve vendor extensions (by storing original XName when harvesting; extend attribute model to record namespace + localName).
- Optional: include a roundtrip metadata block (comment) for versioning.

Phase 8 – Data + collaboration extensions (optional after core)
- Add parsing for <dataObjectReference>, <dataStore>, <dataStoreReference>.
- Add <process><property>.
- Add DataInput / DataOutput (task/subprocess).
- Add conversation / choreography (if needed later).

Phase 9 – Performance & maintainability
- Replace string-keyed documentCache with LRU (size limit e.g. 50).
- Inject IBpmnParseOptions for toggling: StrictValidation, PreserveUnknownExtensions, ParseDiagramInterchange.
- Optionally parse BPMN DI (BPMNShape/BPMNEdge) into separate model for UI.

Incremental coding order (execution-ready after each step):
1. Phase 0 fixes.
2. Introduce new model types (Phase 1) but keep old properties (adapters) to avoid breaking tests.
3. Implement event definition parsing (Phase 3) + adjust tests.
4. Loop characteristics (Phase 4).
5. Validation expansions (Phase 6) guarded by feature flag (e.g. StrictMode).
6. Serialization updates (Phase 7) – add regression snapshot tests.
7. Subprocess containment tagging (Phase 5).
8. Performance & config (Phase 9).
9. Remove deprecated fields after migration window.

Backward compatibility strategy
- Mark old properties [Obsolete] now; keep them mapping to new structures for 1–2 versions.
- Provide a compatibility adapter method: GetPrimaryEventDefinitionKind().

Testing plan
- Unit tests per EventDefinition type (parse + serialize roundtrip).
- Multi-instance: sequential vs parallel with collection vs cardinality.
- Default flow on exclusive/inclusive gateway with conditional vs default scenario.
- Subprocess event start triggers preserved (if engine uses new tagging).
- Negative tests: invalid boundary attachments, missing errorRef, duplicate IDs.
- Snapshot tests: Complex model XML → parse → serialize → normalize (canonical whitespace) → diff.

Minimal code examples

EventDefinition integration (BpmnEvent snippet):
public record BpmnEvent(
    string Id,
    string Type,
    string? AttachedToRef = null,
    bool CancelActivity = true,
    IReadOnlyList<EventDefinition>? Definitions = null,
    Dictionary<string,string>? Attributes = null);

Parsing timer (simplified):
var timer = e.Element(ns + \"timerEventDefinition\");
if (timer != null) defs.Add(new TimerEventDefinition(
    timer.Element(ns + \"timeDate\")?.Value,
    timer.Element(ns + \"timeDuration\")?.Value,
    timer.Element(ns + \"timeCycle\")?.Value));

Default flow:
var defaultFlowId = gatewayElem.Attribute(\"default\")?.Value;
if (defaultFlowId != null)
    mark that sequenceFlow with IsDefault = true;

Condition expression parse (flow):
var cond = flowElem.Element(ns + \"conditionExpression\")?.Value;
if (!string.IsNullOrWhiteSpace(cond)) flow.ConditionExpression = cond.Trim();

Risk control
- Do not refactor execution engine simultaneously; first stabilize parser output.
- Keep test set green after each phase; add new tests before implementation (TDD).

Summary
Apply phases 0–4 first to unlock richer execution semantics (events, loops). Then proceed with validation and serialization fidelity (phases 6–7). Containment and data artifacts later. This phased approach yields immediate value while converging toward fuller BPMN 2.0 compliance.
Mach es speichere alle Änderungen unter dem Projekt VertexBPMN.Parsing und verändere nicht die alten Implementierung, add new tests before implementation (TDD)

======================
Zusätzliche Lücken (im Vergleich zu ursprünglicher Roadmap):
•	Kein mcp/camunda/zeebe/flowable Extension Harvesting im Unified Parser (nur Grundelemente).
•	Keine Fehler/Eskalation/Signal Referenzauflösung (Lookup-Tabellen).
•	Keine dedizierte JSON/Extension Preservation.
•	Keine Performance-Optimierungen (Streaming, LRU Cache).
•	Kein Handling für multiple StartEvents mit Priorisierung (nur simple Not-Found-Diagnostic).
•	Multi-Instance Validierungsregel (cardinality & collection) fehlt.
Handlungsempfehlungen (priorisiert):
1.	Ergänze Multi-instance Konfliktprüfung (collection UND loopCardinality).
2.	Refaktor ParseAsync in klaren Schritte (Maintainability).
3.	Vendor Extension-Harvesting (optional Adapter aus altem Parser wiederverwenden).
4.	Ergänze Referenz-Validierung (ErrorRef, EscalationRef, SignalRef).
5.	Add Validation für Transaktions-Ende (cancel/terminate constraints).
6.	Re-Integrate Extension Preservation (Namespace + Name + Value Liste).
7.	Snapshot Roundtrip Tests (Original XML vs Serialisiert normalisiert).
Fazit: Kernfunktionen Phasen 1–5, Teile 6–8 sind integriert. Validierungs- und Extensions-Tiefe aus Phase 6/7/8 nur teilweise. Liste oben zeigt präzise, was fehlt.
#################################################
