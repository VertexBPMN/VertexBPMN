# A02 – Prüfbericht und verbleibende Abnahme

Stand: 2026-09-14. **Der Implementierungsumfang von A02 ist abgeschlossen.** Keine Produktionsfreigabe und kein Nachweis eines vollständigen Agent-Anwendungsfalls: Worker-Leases, Completion und deren Rennen gehören zu A03/A04.

## Implementiert und lokal geprüft

| Bereich | Nachweis / Code |
| --- | --- |
| Atomare Job-/Wait-/Historienanlage, Idempotenz, Revisionsschutz | `ExternalTaskSchedulingStore.cs`, `ExternalTaskSchedulingConcurrencyTests.cs`, `ExternalTaskSchedulingPreviewTests.cs` |
| Reale PostgreSQL-Neuanlage und Upgrade, konkurrierender Start mit identischem Schlüssel über getrennte Connections, Rollback und geschützter Downgrade | `tests/VertexBPMN.Tests/Acceptance/ExternalTaskPostgresAcceptanceTests.cs`: zwei Fälle, jeweils mit eigener temporärer Datenbank |
| Interrupting/non-interrupting Timer-, Message- und Signal-Boundaries, einfache/parallele Tasks; erneute Subscription nach Boundary-Schleife | `PersistentProcessExecutionRuntime.ExternalTasks.cs`, `ExternalTaskBoundaryAcceptanceTests.cs` |
| Eigene Identität eingebetteter Scopes, Scope-Abbruch, interrupting/non-interrupting Message-Event-Subprozess | dieselben Runtime-/Acceptance-Dateien |
| Lokale MI-Inputs, parallele Anlage, nur erste sequenzielle Iteration; Standard-Loop-Eintritt mit testBefore | `PersistentProcessExecutionRuntime.cs`, `ExternalTaskBoundaryAcceptanceTests.cs` |
| Ungültige Inputs erzeugen redigierten Incident ohne Job; Definition/Mappings, Inputs und Schema erhalten SHA-256-Snapshots | `PersistentProcessExecutionRuntime.cs`, Boundary- und SchedulingPreview-Tests |
| Normalisierter und Strict-Roundtrip, Streaming mit External-Mappings, Scope-Zuordnung und MI-Struktur | `BpmnStreamingParser.cs`, `BpmnParser.cs`, `BpmnSerializer.cs`, `ExternalTaskDefinitionTests.cs` |
| Capability-Prüfung aufgerufener Modelle vor Service-Seiteneffekten; Simple/Distributed lehnen External Tasks ab | `ProcessEngine.cs`, `DistributedProcessEngine.cs`, persistente Runtime und Definition-/Boundary-Tests |

Die PostgreSQL-Tests nutzen die produktive Providerkonfiguration `UseVertexNpgsql`. Sie ersetzen keinen generellen Nachweis für alle Migrationen oder beliebige Mehrreplikat-/Crash-Konstellationen. Die synchronisierte Konkurrenzprüfung steuert den Zeitpunkt vor dem INSERT; Unique-Constraint und Konfliktbehandlung laufen auf echtem PostgreSQL, nicht auf einem simulierten Datenbankfehler.

Ein im Test gefundener Runtimefehler wurde im Kern korrigiert: Metadaten eines Event-Subprozess-Triggers wurden auf den tatsächlichen Arbeits-Wait übertragen. Dadurch konnte die Abschlussprüfung diesen Wait fälschlich als passiven Trigger ignorieren und den Prozess trotz offenem Job abschließen. `ExecutionContextFromToken` übernimmt diese Trigger-Markierungen jetzt nicht mehr. Die Tests verlangen weiterhin einen laufenden Prozess mit korrekt zugeordnetem Job.

Ein weiterer echter Roundtripfehler wurde im Serializer behoben: flach projizierte Mapping-Attribute erzeugten ungültige/doppelte XML-Erweiterungen. Mappings werden jetzt strukturiert geschrieben; Task-Loop-Metadaten und Scope-Hierarchie bleiben in den geprüften Roundtrips erhalten.

## Abschließender lokaler Lauf

- `dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --no-restore -c Release --disable-build-servers --maxcpucount:1 -v:q`: erfolgreich, 0 Fehler, 0 Warnungen im **inkrementellen** Abschlusslauf. Der vorherige vollständige Build meldete 32 Warnungen; diese sind damit nicht als behoben anzusehen.
- `./scripts/test-external-tasks-local.ps1 -NoBuild` mit `VERTEXBPMN_TEST_POSTGRES_ADMIN`: **184 Tests, 0 Fehler, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt; 76,450 s**.
- Enthalten sind neue External-Task-Fälle und bestehende Runtime-, Compensation-, Parser-/Serializer-, MI- und Distributed-Regressionen. Dies ist eine gezielte Auswahl, nicht die gesamte Projektsuite oder ein Browser-/Worker-E2E-Test.
- Eigene lokale PostgreSQL-16-Testinstanz auf Loopback-Port 55439; ausschließlich synthetische Daten. Testdatenbanken werden im jeweiligen finally-Block entfernt. Keine CI-Änderung.

## Arbeiten nach A02 in Umsetzungsreihenfolge

1. **A03: Lease-/Worker-Protokoll implementieren.** Tenant-/Worker-Autorisierung, Claim, Lease-Verlängerung und Fencing auf dem bestehenden Persistenzmodell aufbauen. Keine öffentlichen Completion-Erfolge vortäuschen.
2. **A04/E09: Completion und Fortsetzung implementieren.** Ergebnis gegen Snapshot prüfen, lokale Outputmappings anwenden, sequenzielle MI und Standard-Loops fortsetzen, parallelen Join korrekt abschließen. A02 belegt Scheduling und Abbruch, nicht den vollständigen MI-/Loop-Lebenszyklus.
3. **A04/E10: Konkurrenz-/Neustartabnahme.** Timer gegen Completion, Cancellation gegen laufenden Worker, mehrere Replikate sowie Crash nach Receipt und vor Fortsetzung testen. Genau eine konsistente Zustandsentscheidung und keine doppelte Fortsetzung nachweisen. `AdvanceAsync` darf wegen möglicher Service-Seiteneffekte nicht blind wiederholt werden.
4. **A05/A06:** produktionsfähige Schema-/Policyauflösung einschließlich verschachtelter Resultate, Widerruf, Retention/Purge und Ressourcenlimits ergänzen. Der lokale skalare Katalog und einfache Variablenmappings bleiben ausdrücklich begrenzt.

Der bestehende Plan verlangt paketübergreifend E01/E02/E09/E10. Diese Kriterien werden nicht abgeschwächt: E01 und der Scheduling-/Abbruchanteil von E02/E09/E10 sind belegt; vollständige E09/E10 hängen von A03/A04 ab und bleiben bis dahin offen.

## Nachtrag: verschachtelte Incident-Recovery

`PersistentProcessExecutionRuntime.SuspendWithIncidentAsync` persistiert bei External-Task-Validierungsfehlern vorhandenen lokalen Ausführungskontext in einem Failed-Token vom Typ `incidentRecovery`. Dessen ID entspricht exakt der Incident-ID; parallele Iterationen werden damit nicht allein anhand der Activity-ID zugeordnet. Ohne lokalen/MI-Kontext bleibt der bisherige einfache Recoverypfad bestehen. Fehlermeldung und Incident-Outbox enthalten weiterhin nur den redigierten Fehlercode, nicht den Kontext.

`RecoverIncidentAsync` lädt diesen Kontext innerhalb derselben Transaktion und entfernt den alten Recovery-Token. Scheitert die Eingabeprüfung erneut, entsteht ein neuer Incident mit erneut persistiertem Kontext. Der normale External-Task-Wait wird erst bei erfolgreicher Validierung angelegt.

Nachweis: `ExternalTaskBoundaryAcceptanceTests.NestedIncidentRecoveryPreservesScopeAcrossReloadAndRepeatedFailure` startet einen echten eingebetteten Subprozess mit fehlendem Input, leert den Change Tracker, führt einen weiterhin ungültigen Retry aus, korrigiert die persistierte Prozessvariable und ruft die Recovery zweimal mit identischem Idempotenzschlüssel auf. Erwartet werden unveränderte Scope-ID, genau ein Job/Wait, laufender Prozess und aufgelöste Incidents. Kein Handlerdouble ersetzt die Ausführung; nur der nicht aufzurufende Inline-Handler wird streng überwacht. Das Leeren des Change Trackers ist kein Prozessabsturz-Nachweis.

Zwischenstand dieses Nachtrags: Release-Build mit **0 Fehlern, 32 Warnungen**. Gezielter Lauf der Klassen `ExternalTaskBoundaryAcceptanceTests`, `ExternalTaskSchedulingPreviewTests`, `PersistentRuntimePhase2AcceptanceTests`: **44 bestanden, 0 Fehler/Fehlschläge/Skips/nicht ausgeführt, 13,338 s**. PostgreSQL und die damalige 184er-Auswahl wurden an dieser Stelle noch nicht erneut ausgeführt. Die abschließende Bewertung steht unten.

## Nachtrag: öffentliches Suspend/Resume

In `src/VertexBPMN.Application/RuntimeService.cs` konnte `ResumeAsync` einen Prozess im Zustand `Incident` ohne Incident-Recovery auf Running setzen. Dieser Pfad ist jetzt vor Zustandsänderung und Eventausgabe gesperrt. Normales administratives Suspend/Resume bleibt unverändert möglich.

Zwei neue Fälle in `ExternalTaskBoundaryAcceptanceTests` verwenden den echten Application-`RuntimeService`, EF-Repository und die persistente Engine:

- `PublicSuspendPreservesExternalWaitAndResumeAllowsBoundaryCancellation`: Suspend erhält Job und Wait, eine Message-Correlation im suspendierten Zustand wird abgewiesen. Resume erhält dieselben IDs. Die danach erneut gesendete Message mit demselben Idempotenzschlüssel cancelt den Job über die interrupting Boundary und beendet den Prozess. Der zuvor abgewiesene Versuch darf keinen erfolgreichen Inbox-Eintrag hinterlassen.
- `PublicResumeCannotBypassExternalTaskIncidentRecovery`: Resume bei offenem Input-Incident wird abgewiesen, ohne Statusänderung, Jobanlage oder Mining-Event.

Release-Build: **0 Fehler, 31 Warnungen**. Dieselben drei Testklassen wie im Recovery-Nachtrag: **46 bestanden, 0 Fehler/Fehlschläge/Skips/nicht ausgeführt, 17,366 s**. Kein erneuter PostgreSQL-/Gesamtlauf. Dies belegt noch keine Suspend/Claim-/Completion-Rennen; die Worker-API fehlt weiterhin.

Die Inspektion bestätigt weitere offene Lebenszyklusprobleme: `RuntimeService.EndProcessAsync` emittiert derzeit lediglich ein Mining-Event; `ProcessInstanceRepository.DeleteAsync` versucht eine direkte Prozesslöschung. Diese Methoden wurden in diesem Schritt nicht als vollständiger External-Task-Abbruch beziehungsweise retentiongerechte Löschung umgedeutet. Dafür bleiben atomare Abbruchintegration, Receipt-Aufbewahrung und explizite öffentliche API-Tests erforderlich.

## Abschluss 2026-09-14: Lifecycle, MI-Recovery und A02-Abnahme

Die zuvor offenen A02-Lebenszykluspfade sind umgesetzt:

- `IProcessExecutionRuntime.TerminateAsync` terminiert Prozess, External Jobs, offene Attempts, Pending Continuations, Wait-Tokens, User Tasks, Timer, Subscriptions, MI-Ausführungen und Incident-Recovery-Zustände in derselben relationalen Transaktion. Wiederholung auf einem terminalen Prozess erzeugt keine zweite Historie.
- `RuntimeService.EndProcessAsync` verwendet diesen Abbruchkern statt nur ein Mining-Event auszugeben.
- `RuntimeService.DeleteAsync` terminiert zuerst. Gibt es External-Task-Auditdaten, bleiben Prozess und Datensätze als terminaler Retention-Tombstone erhalten; das Mining-Event lautet `ProcessTerminated`. Ohne External-Auditdaten bleibt das bisherige physische Löschen erhalten und wird mit `ProcessDeleted` gemeldet. Ein späterer Purge unterliegt A05/A06-Retention und ist nicht Teil von A02.
- Drei gleichzeitig fehlschlagende parallele MI-Iterationen erhalten eine gemeinsame MI-Ausführung, getrennte Indizes und je Incident einen eigenen Recovery-Token. Nach Korrektur des Inputs erzeugt jede Recovery genau einen Job mit eigener ActivityExecution-/Wait-ID. Wiederholte Recovery mit demselben Idempotenzschlüssel dupliziert nichts.

Neue Nachweise in `ExternalTaskBoundaryAcceptanceTests` prüfen den öffentlichen Delete-Pfad für drei External Jobs, den Abschluss eines verschachtelten Incident-Recovery-Zustands, drei parallele MI-Recoveries sowie die unveränderte physische Löschung eines Prozesses ohne External-Auditdaten. Die früheren Root-Terminate-Fälle decken zusätzlich Leased Jobs und bereits angenommene Ergebnisse mit Pending Continuation über denselben Abbruchkern ab.

Abschließende Verifikation:

- Finaler inkrementeller Release-Build: **0 Fehler, 0 ausgegebene Warnungen**; der vorherige vollständige Build meldete weiterhin **31 bestehende Warnungen**, daher keine Warnungsbereinigung behauptet.
- `ExternalTaskBoundaryAcceptanceTests`: **33 bestanden**, 0 Fehler/Fehlschläge/Skips/nicht ausgeführt, 5,944 s.
- `scripts/test-external-tasks-local.ps1 -NoBuild` mit realem lokalem PostgreSQL und zusätzlich `ManagementServiceTests`: **195 bestanden**, 0 Fehler/Fehlschläge/Skips/nicht ausgeführt, 79,327 s.
- Ein separater sequenzieller Lauf der gesamten Test-DLL wurde nach ungefähr sechs Minuten ohne Endergebnis manuell beendet, um den A02-Abschluss nicht unbegrenzt zu blockieren. Er ist ausdrücklich **kein** grüner Gesamtsuite-Nachweis. Die definierte A02-Suite wurde danach mit den finalen Binärdateien vollständig grün ausgeführt.

**A02-Urteil:** Der in A02 definierte Implementierungsumfang für Domain, Migration, Parser/Serializer, Deploymentvalidierung, atomare Job-/Wait-Anlage, Scope-/LocalVariables, Recovery und Abbruch ist abgeschlossen. E01 sowie die A02-relevanten Scheduling-/Abbruchanteile von E02/E09/E10 sind belegt. Vollständige Worker-Completion, Loop-/MI-Fortsetzung sowie Completion-/Timeout-/Crash-Rennen bleiben unverändert A03/A04 und verhindern weiterhin eine Produktionsfreigabe des gesamten Features.
