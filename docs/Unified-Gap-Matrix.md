# Unified Parser Feature Gap Matrix (Snapshot Phase A)

Status Codes:
- ? Implemented (usable)
- ?? Partial (basic support, missing depth)
- ? Missing

## 1. Events & Event Definitions
| Feature | Status | Notes |
|---------|--------|-------|
| startEvent / endEvent | ? | Parsed into Events list |
| intermediateCatch / Throw | ? | Types distinguished via `Type` field |
| boundaryEvent (generic) | ? | Parsed + attachedToRef validation (only existence) |
| Event Subprocess flag (`triggeredByEvent`) | ? | Subprocess record has IsEventSubprocess |
| timerEventDefinition (timeDate/timeDuration/timeCycle) | ?? | Elements captured, no semantic timing evaluation |
| messageEventDefinition (messageRef) | ?? | Ref captured as raw attribute; no global message registry |
| signalEventDefinition (signalRef) | ?? | Same as message |
| errorEventDefinition (errorRef) | ?? | No error catalog / binding validation |
| escalationEventDefinition | ?? | Ref string only, no resolution |
| compensateEventDefinition (activityRef) | ?? | No validation that activityRef exists/compensatable |
| conditionalEventDefinition (conditionExpression) | ? | Expression text captured |
| linkEventDefinition | ?? | Name captured; only unmatched detection; no directional throw/catch semantics |
| terminateEventDefinition | ?? | Parsed, no validation context (transaction scope) |
| cancelEventDefinition | ?? | Parsed, not restricted to transaction subprocess |
| Multiple definitions on one event | ? | All child definitions aggregated |
| Throw vs Catch distinction (link, escalation, signal, message) | ? | Not inferred |

## 2. Activities / Tasks / Subprocess
| Feature | Status | Notes |
| Generic *Task parsing | ? | Task Id/Type/SubprocessId captured |
| User / Service / Script task specialization | ? | No specialized attribute extraction in Unified parser (legacy parser had some) |
| callActivity calledElement | ? | Not parsed |
| Subprocess containment (SubprocessId) | ? | Flattened w/ stack; no hierarchical object tree |
| Event Subprocess start validation (typed) | ? | Validates typed start presence |
| Transaction subprocess (transaction attr) | ?? | Flag captured; no cancel/compensation enforcement |
| AdHoc subprocess | ?? | Recognized but no ad-hoc semantics / ordering |

## 3. Multi-Instance & Loop
| Feature | Status | Notes |
| multiInstanceLoopCharacteristics (sequential flag) | ? | isSequential parsed |
| loopCardinality | ? | Parsed integer |
| completionCondition | ? | Text captured |
| collection (camunda:collection) | ? | Captured; overrides cardinality |
| elementVariable (camunda:elementVariable) | ? | Chosen before zeebe inputElement/outputElement |
| zeebe input/output element mapping | ?? | Fallback extraction only; not stored separately |
| Conflict validation (cardinality + collection) | ? | Missing (was in earlier plan) |
| standardLoopCharacteristics | ? | loopCondition/testBefore/loopMaximum captured |

## 4. Gateways & Sequence Flows
| Feature | Status | Notes |
| Gateway typing (exclusive/parallel/etc.) | ? | Type stored |
| Default flow detection | ? | `IsDefault` flag on flows |
| SequenceFlow conditionExpression | ? | Text captured (no evaluation) |
| Priority on flows | ? | Not parsed |
| Outgoing flow count validation | ? | Not enforced |

## 5. Data & Artifacts
| Feature | Status | Notes |
| dataObject | ? | Id + name |
| dataObjectReference | ? | dataObjectRef captured (no cross-check) |
| dataStore | ? | Id + name |
| dataStoreReference | ? | dataStoreRef captured (no validation) |
| process properties (<property>) | ? | Id + name |
| Activity IO (dataInput/output + associations) | ? | Flattened per activity |
| Data associations semantic validation | ? | Not implemented |
| DataInput/Output sets / mapping semantics | ? | Not implemented |

## 6. Validation Rules
| Rule | Status | Notes |
| Duplicate IDs | ? | Diagnostic added |
| Boundary attachedTo target existence | ? | Checks tasks/subprocesses only |
| SequenceFlow endpoint existence | ? | Basic check |
| Default flow must not have condition | ? | Implemented |
| Missing startEvent in process | ? | Diagnostic |
| Event subprocess needs typed start | ? | Implemented |
| Unmatched link events | ? | Simple count (1=error) |
| MI cardinality+collection conflict | ? | Missing |
| Cancel/Terminate placement validation | ? | Missing |
| Compensation boundary restrictions | ? | Missing |
| Link Throw vs Catch pairing semantics | ? | Missing (no direction classification) |
| At least one outgoing flow per gateway | ? | Missing |
| Error / Escalation ref resolution | ? | Missing |

## 7. Vendor Extensions
| Feature | Status | Notes |
| Camunda extensionElements (properties, formFields) | ? | Not in Unified parser |
| Zeebe taskDefinition / ioMapping | ? | Not parsed |
| Flowable / Activiti / jBPM / CIB / Osmanthus / Alfresco | ? | Legacy-only |
| Generic namespace preservation | ? | Not implemented |

## 8. Serialization Fidelity
| Feature | Status | Notes |
| Event definitions serialization | ? | In Unified serializer |
| ConditionExpression CDATA | ? | Implemented |
| Loop serialization (MI + Standard) | ? | Basic fields only |
| Vendor extensions roundtrip | ? | Not present |
| Namespace pruning/dedup | ? | Not handled |
| Comment metadata (serialized-by) | ? | Not added |

## 9. Performance & Options
| Feature | Status | Notes |
| Parser options (Strict, PreserveExtensions, etc.) | ? | Not present |
| Document cache / LRU | ? | Not present |
| Streaming (XmlReader incremental) | ? | Not present |

## 10. Diagram Interchange (DI)
| Feature | Status | Notes |
| BPMNShape parsing | ? | Not implemented |
| BPMNEdge parsing | ? | Not implemented |
| Waypoints preservation | ? | Not present |

## 11. Engine Integration Readiness
| Feature | Status | Notes |
| Flattened graph consumption | ? | Lists available |
| Scope/hierarchy reconstruction | ?? | Via SubprocessId only |
| Event definition semantics (execution triggers) | ? | No enrichment layer |

## 12. Backward Compatibility
| Feature | Status | Notes |
| Legacy phase parsers kept | ? | Still in repo (not Obsolete) |
| Unified model invariants documented | ? | Pending doc |
| Migration guide | ?? | Partial plan file only |

## 13. Priorisierte To?Dos (Next Phases)
1. (Phase B) Implement: MI conflict check, cancel/terminate rules, compensation attachment, gateway outgoing check, link role detection.
2. (Phase C) Add reference catalogs (messages, signals, errors, escalations) + resolution diagnostics.
3. (Phase D) Reintroduce vendor extension harvesting + serializer preservation.
4. (Phase F) Expand MI model (separate Input/Output/ElementVariable).
5. (Phase E) Flow priority parsing.
6. (Phase G/H) Hierarchical children + extension roundtrip snapshots.
7. Options & Performance (Phase I) then DI (Phase J) if needed.

---
Generated Phase A snapshot. Update this matrix after completing each subsequent phase.
