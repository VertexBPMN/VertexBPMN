# External Tasks und Agent-Vertragsprüfung – A04-Prüfbericht

Stand: 2026-09-14. Paket: A04 – Completion, Recovery und BPMN-Semantik. Basiscommit: `b41fe40cd2a36c800af92a86ed4fae47e7dfac51` auf `codex/external-agent-a00-inventory`; A04 ist in diesem Arbeitsstand noch nicht committed.

## Urteil

Der generische External-Task-Lebenszyklus ist für den vereinbarten A04-Umfang vollständig implementiert: Worker können Ergebnisse oder redigierte Fehler melden; der Server bindet jede Mutation an Tenant, Topic, Profil, aktuellen Policy-Snapshot, `(iss, sub)`, Lease-ID, Lease-Generation und ActivityExecution-ID. Ergebnisannahme und Pending-Continuation werden atomar gesichert. Ein separater idempotenter Consumer übernimmt Output und BPMN-Fortsetzung und kann nach einem Prozessneustart weiterarbeiten.

Dies ist noch keine Produktionsfreigabe des Agent-Anwendungsfalls. A05 bis A07 – konkrete Modellruntime, Studio/Betrieb und fachliche Gesamtqualifikation – bleiben offen. `ExternalTasks:EnableSchedulingPreview` bleibt standardmäßig deaktiviert und außerhalb Development/Test weiterhin fail-closed.

## Implementierter Umfang

- `ExternalTaskLeaseService` implementiert Complete/Fail, kanonische SHA-256-Receipts, identische Wiederholung, Konflikterkennung, Current-Policy-Prüfung sowie atomare Job-/Attempt-/Continuation-Transitions.
- `ExternalTaskPayloadPolicy` begrenzt Ergebnisse auf 128 KiB und Tiefe 32, verwirft doppelte JSON-Properties, prüft den gespeicherten skalaren Outputvertrag und kanonisiert Objektkeys deterministisch. Zahlenlexeme und Arrayreihenfolge bleiben erhalten.
- `ExternalTaskRecoveryService` verarbeitet absolute Deadlines, abgelaufene Leases und fällige Retries in Batches bis 100 mit Prozess-/Wait-/Job-CAS und kurzen Einzeltransaktionen.
- `ExternalTaskRecoveryHostedService` führt alle fünf Sekunden Recovery und Pending-Continuation/Dispatch aus, wiederholt Konflikte höchstens dreimal und begrenzt Datenbankfehler-Backoff auf 60 Sekunden. Jeder Pass verwendet einen frischen DI-Scope.
- `PersistentProcessExecutionRuntime` übernimmt Root- und Local-Scope-Outputs, Error-Boundaries, technische Incidents, Standard Loops sowie sequenzielle und parallele Multi-Instance-Fortsetzungen. Annahme und nachfolgende normale Runtime-Ausführung sind durch persistente Dispatch-Tokens getrennt.
- Der Worker-Host besitzt einen strukturierten Handler-Resultvertrag und sendet Complete/Fail ausschließlich selbst. Verlorene Antworten werden mit derselben Receipt-ID und demselben Payload nur innerhalb der gültigen Lease wiederholt. Leaseverlust und Shutdown verhindern spätere Mutation.
- Die API besitzt strikt deserialisierte Complete-/Fail-Verträge und stabile 400/403/404/409/413/422-ProblemDetails ohne Exceptiontext.

## Nachgewiesene Semantik

Die Acceptance- und Unit-Tests belegen:

- identische Completion und identisches Fail-Receipt sind idempotent; abweichende IDs oder Payloads kollidieren;
- fremder Worker, widerrufene Policy, alte Lease und alte Generation erhalten keinen erfolgreichen Receipt;
- Schemafehler, doppelte JSON-Properties und nicht erlaubte Business Errors verändern Job, Attempt und Continuation nicht;
- fachlicher Fehler erreicht den passenden BPMN Error Boundary; technische Erschöpfung erzeugt einen redigierten Incident;
- Success schreibt Root-Output dauerhaft und setzt den normalen Pfad genau einmal fort;
- Standard Loop sowie sequenzielle und parallele Multi-Instance-Ausführung schließen alle External-Task-Iterationen ab;
- interrupting Timer und Completion besitzen jeweils einen konsistenten Gewinner; verspätete Completion schreibt keinen Output;
- Termination nach dauerhaftem Receipt, aber vor Apply, annulliert die Continuation und behält Audit-/Receipt-Daten ohne wirksamen Output;
- Lease-Ablauf ohne Worker-Fail führt über deterministischen Backoff zu Reclaim oder bei Erschöpfung/Deadline zu terminaler Continuation;
- zwei echte PostgreSQL-DbContexts claimen denselben Job nicht doppelt. Konkurrierende identische Completion erzeugt genau eine Continuation; eine neue Runtime/DbContext-Instanz appliziert sie nach simuliertem Neustart genau einmal.

## Verifikation

- Release-Builds der geänderten Projekte `Domain`, `Infrastructure`, `Engine`, `Api` und `AgentWorker`: jeweils **0 Fehler, 0 Warnungen**.
- Release-Testbuild mit deaktivierten ProjectReferences nach separatem Build der geänderten Projekte: **0 Fehler, 24 bestehende Warnungen**.
- Release `-class '*ExternalTask*'`: **152 gesamt, 149 bestanden, 0 fehlgeschlagen, 3 PostgreSQL-Tests ohne gesetzte lokale Verbindung erwartungsgemäß übersprungen**.
- Derselbe Release-Artefaktstand gegen WSLC PostgreSQL 17 auf Loopback: `ExternalTaskPostgresAcceptanceTests` **3/3 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Die zuvor gestoppten WSLC-Container wurden ausschließlich für den Lauf gestartet und danach wieder gestoppt.
- Persistente Runtime-/Compensation-/MI-Regression: **12/12 bestanden**.
- Parser-/Serializer-Regression: **9/9 bestanden**.
- `git diff --check`: keine Whitespacefehler; lediglich erwartete Git-Hinweise zur LF/CRLF-Konvertierung.

Der vollständige Release-Solution-Build konnte lokal nicht als grün gewertet werden: MSBuild erhielt beim Erzeugen temporärer Dateien unter `src/VertexBPMN.Cli/obj/Release` und `src/VertexBPMN.Sdk/obj/Release` `Access denied` (`MSB3491`). Die geänderten A04-Projekte wurden anschließend einzeln erfolgreich gebaut. Es wurden keine Verzeichnisse gelöscht und keine Berechtigungen gewaltsam verändert.

## Nächster Schritt

A05 implementiert den konkreten read-only Vertragsprüfungsagenten. Dabei bleiben External-Task-Transport und Modellruntime getrennt: Der Handler erhält keinen API-Client, keine Shell, kein allgemeines HTTP und keinen freien Dateisystemzugriff. Erst A07 darf aus technischer und fachlicher Abnahme eine Freigabeentscheidung ableiten.
