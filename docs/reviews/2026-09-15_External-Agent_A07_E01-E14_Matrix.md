# External Agent A07 – E01 bis E14 Nachweismatrix

Stand: 2026-09-16. Diese Matrix bezieht sich auf den Arbeitsstand des Branches
`codex/external-agent-a00-inventory`. A07 wurde vom Auftraggeber nach einem eigenen lokalen Test
abgenommen und als separate Phase übersprungen. „Bestanden“ bedeutet nur den jeweils genannten
automatisierten Nachweis, nicht automatisch eine vollständige Produktionsfreigabe.

| ID | Status | Konkreter Nachweis | Verbleibende Grenze |
|---|---|---|---|
| E01 | Bestanden | `ExternalTaskDefinitionTests`, `ExternalTaskDeploymentTests`, `BpmnEditorInsertionTests`; Moddle-/XML-Roundtrip, Limits, Input-/Output-Mappings und Editor Undo/Redo. | Keine. |
| E02 | Bestanden | `ExternalTaskPostgresAcceptanceTests.MigratedPostgresSchedulesOneWaitForConcurrentPublicStarts`; echte PostgreSQL-Verbindungen und konkurrierende Starts erzeugen genau einen Wait/Job. | Keine. |
| E03 | Bestanden | `ExternalTaskPostgresAcceptanceTests.TwoDatabaseWorkersHaveOneLeaseWinnerAndOneDurableCompletion`; zwei DbContexts/Verbindungen, genau ein Lease-Gewinner. | Kein Multi-Host-Lasttest; für den vereinbarten lokalen Fall ausreichend. |
| E04 | Bestanden | `ExternalTaskLeaseServiceTests.ExpiredLeaseIsRecoveredWithBackoffThenReclaimedWithNewFence` sowie Claim-/Heartbeat-/Complete-Negativpfade; alte Lease/Generation verliert. | OS-Prozessneustart wird zusätzlich in E11 gefordert. |
| E05 | Bestanden | `ExternalTaskLeaseServiceTests.CompleteAtomicallyStoresCanonicalResultReceiptAndPendingContinuation`, Duplicate-/Schema-Negativtest und API-ProblemDetails; identische Wiederholung idempotent, Abweichung abgewiesen. | Keine. |
| E06 | Lokal abgenommen | Atomare Grenzen und Cancellation sind in `ExternalTaskCompletionAcceptanceTests` deterministisch geprüft, einschließlich Receipt-vor-Apply und Timer/Completion-Race; zusätzlich lokale Auftraggeberabnahme. | Kein aufgezeichneter automatisierter Hard-Crash an allen Commit-Failpoints in getrennten OS-Prozessen. |
| E07 | Bestanden | `ExternalTaskApiContractTests`, `JwtBearerHandlerOidcTests`, `ExternalTaskLeaseServiceTests`; Tenant, Topic, Profil, Subject, Claim und aktuelle Policy werden je Operation geprüft. | Keine. |
| E08 | Bestanden | `ExternalTaskLeaseServiceTests.RecoveryMakesExpiredLastAttemptAndDeadlineTerminal`, `ExternalTaskWorkerServiceTests.IndependentHeartbeatCancelsLongWorkWhenLeaseIsLost` und technische Incident-Acceptance; Retry/Deadline bleiben begrenzt. | Keine. |
| E09 | Bestanden | `ExternalTaskBoundaryAcceptanceTests` und `ExternalTaskCompletionAcceptanceTests`; Standard Loop sowie sequenzielle/parallele Multi-Instance mit lokalen Outputs und Join. | Keine. |
| E10 | Bestanden | `AllowedBusinessErrorUsesMatchingBoundaryWithoutSuccessOutput`, beide Timer/Completion-Race-Richtungen, Cancellation/Event-Subprocess/Scope-Tests. | Keine. |
| E11 | Lokal abgenommen | `ExternalAgentProcessRecoveryAcceptanceTests` und `scripts/test-external-agent-recovery-local.ps1` implementieren Worker-/API-Hard-Crash, PostgreSQL-Ausfall, Volume-Neustart und Drain; zusätzlich lokale Auftraggeberabnahme. | Der automatisierte Runner war auf diesem Host wegen instabiler WSLC-Portweiterleitung nicht grün; kein automatisierter Erfolg wird behauptet. |
| E12 | Bestanden | `ContractReviewExternalTaskHandlerTests.E12_prompt_injection_cannot_add_tools_or_remove_human_review`; nur `read_section`/`search_document`, Human Review serverseitig erzwungen. CRB-013 bis CRB-015 ergänzen reale Modellfälle nach Expertenfreigabe. | Fachbenchmark noch nicht freigegeben. |
| E13 | Bestanden | Oversize-, erfundene Evidence-, falsche Version-, Schema-, Response-Limit- und einmalige Repair-Tests in `ContractReviewExternalTaskHandlerTests` und `ExternalTaskLeaseServiceTests`. | Keine. |
| E14 | Bestanden | `LocalStudioInfrastructureTests.ExternalAgentPreset_DeploysAndShowsProcessScopedWaitingJob`: reales WSLC/PostgreSQL, API und Studio; 1/1, keine Skips. | Der Test endet absichtlich beim persistenten Ready-Job; die reale Modellqualität wird separat im Fachbenchmark gemessen. |

## Aktuelle Läufe

- Technische Gesamtregression mit WSLC-PostgreSQL und echtem `qwen3:8b`: **269/269**, keine
  Fehler, keine Skips, 193,035 s.
- Reales einzelnes Ollama-Dokument: **1/1**, keine Skips, 114,863 s.
- Benchmark-Manifest/Beispielprozess: **1/1**, keine Skips.
- 20-Dokumente-Fachrunner: erwartungsgemäß fail-closed vor Inferenz, da die technische
  Referenzannotation noch keine fachkundige Freigabe besitzt.
- E11-Prozessrunner: implementiert, aber lokal nicht grün. Mehrere Läufe erreichten die echten
  Hard-Crash-/Datenbankausfallpfade; die finale Wiederholung scheiterte bereits beim ersten
  EF-Migrationszugriff mit `NpgsqlException`/`Timeout during reading attempt`, obwohl die
  vorherige PostgreSQL-Protokollprobe erfolgreich war. Das ist kein bestandener E11-Nachweis.
- Abschließende deterministische Auswahl für Deployment/Produktions-Gate, Completion,
  Lease/Recovery, Worker, Agent-Handler, Operations-API, Keycloak und AppHost: **85/85**,
  0 Fehler, 0 Skips, 8,863 s.

## Vom Auftraggeber akzeptierte Restrisiken

1. Kein aufgezeichneter automatisierter E06-Hard-Crash-Lauf an allen Commitgrenzen.
2. Kein grüner automatisierter E11-Runner auf diesem WSLC-Host; der Auftraggeber hat den
   Anwendungsfall separat lokal getestet.
3. Keine fachkundige Freigabe der CRB-v1-Annotationen und kein daraus folgender realer
   20-Dokumente-Fachbenchmark.
4. Infrastrukturabhängige Akzeptanztests bleiben lokale Suiten und laufen nicht in GitHub CI.
