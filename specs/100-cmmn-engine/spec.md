# Spec – CMMN 1.1 Engine (Flowable-OSS-Parität)

## Ziel
Case-Management-Ausführung gemäß CMMN 1.1 inkl. Stages, Tasks, Sentries (onParts/ifPart), Repetition, Reactivation, Event/Timer Listener, Milestones.

## Musskriterien
- CaseDefinition/CaseInstance Lebenszyklus mit PlanItem-Lifecycle
- Sentries: Entry/Exit mit onParts (Event, Timer) und ifPart (Expression)
- Repetition (counter, collection), Manual Activation Rule
- Tasks: HumanTask, ProcessTask, CaseTask, DecisionTask (DMN), Stage
- Event Listener (Message/Signal/Timer), Milestones
- REST: case-definition, case-instance, plan-item-instance, history (UTC)
- Tenancy: alle Operationen tenant-aware
- Observability: Traces (Start/Complete/Terminate), Metriken (Counts, Durations)

## Abnahme
- Contract-Tests für alle REST-Routen grün
- Integration: Referenz-Cases (Happy, Sentry-Exit, Repetition, Reactivation) grün
- History: Vollständiger Audit-Trail, Cleanup-fähig
- Performance: ≥ 500 Case-Starts/s p95 < 200ms (ohne externe I/O)

## Nichtziele
- UI-Apps (Work/Engage) – nicht Teil der OSS-Parität
