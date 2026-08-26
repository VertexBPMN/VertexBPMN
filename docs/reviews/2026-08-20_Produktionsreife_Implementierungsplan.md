# Implementierungsplan zur Produktionsreife

**Projekt:** VertexBPMN  
**Stand:** 20.08.2026  
**Ausgangsurteil:** Nicht produktionsreif  
**Ziel:** Ein belastbarer, klar abgegrenzter BPMN-Produktionskern mit reproduzierbarem Build, dauerhafter Ausführung und überprüfbaren Release-Gates.

## Leitlinie

Der erste produktive Meilenstein konzentriert sich auf einen ausdrücklich definierten BPMN-Subset. CMMN, vollständiges FEEL, Migration, Simulation und Predictive Analytics werden erst als produktionsreif ausgewiesen, wenn ihre End-to-End-Funktion und Konformität separat nachgewiesen sind.

## Priorisierter Implementierungsplan

| Reihenfolge | Titel und Feature | Konkreter technischer Lösungsansatz | Aufwand | Priorität | Abhängigkeiten |
|---:|---|---|---|---|---|
| 1 | Produktumfang und Acceptance Matrix | Für BPMN, DMN und CMMN jedes beworbene Element als `supported`, `partial` oder `unsupported` klassifizieren. README und APIs auf den tatsächlich unterstützten Umfang reduzieren. Verbindliche End-to-End-Akzeptanzfälle definieren. | S | Muss | – |
| 2 | Reproduzierbarer Build und Test-Runner | Restore-Abbruch im Projektgraphen diagnostizieren; Linux- und Windows-Artefakte strikt trennen; SDK-Version in `global.json` pinnen; sauberen Checkout in CI bauen. Tests über den korrekten Microsoft-Testing-Platform-Runner starten. | M | Muss | 1 |
| 3 | Rote End-to-End-Vertragstests | Tests für Deploy → Start → Service/User Task → Complete → End, Timer-Wait, Message/Signal, Neustart und parallele Instanzen erstellen. Echten API-Host und relationale Testdatenbank verwenden, keine gemockte Runtime. | M | Muss | 1–2 |
| 4 | Einheitlicher ausführbarer Runtime-Pfad | `RepositoryService` muss sicher parsen, validieren und versionieren. `RuntimeService.StartProcessByKeyAsync` muss die gewählte Engine starten und dauerhafte Tokens erzeugen. Lokale und Distributed Engine entweder vereinigen oder über ein klares `IProcessExecutionRuntime` kapseln. | L | Muss | 3 |
| 5 | Dauerhafte Zustandsmaschine | Tokens, Variablen, Subscriptions, User Tasks, Jobs, Worker und Incidents relational persistieren. Transaktionen plus Outbox/Inbox, Optimistic Concurrency, Idempotency Keys, Lease/Locking und Retry-/Dead-Letter-Semantik implementieren. | L | Muss | 4 |
| 6 | Produktions-DI bereinigen | Beim Start im Production-Modus hart fehlschlagen, wenn Fake-, In-Memory- oder No-op-Implementierungen aufgelöst werden. Persistent Mining Sink, echter Dispatcher/Broker und persistenter Worker Store registrieren. | M | Muss | 4–5 |
| 7 | Script-, Connector- und Plug-in-Isolation | C#-Scripts standardmäßig deaktivieren. Falls benötigt, in separatem Worker/Container mit CPU-, Speicher-, Zeit-, Dateisystem- und Netzwerkgrenzen ausführen. Ausgehende HTTP-/SMTP-/DB-Ziele unabhängig von Credentials allowlisten; private und link-local Ziele blockieren. Plug-ins signieren oder allowlisten. | L | Muss | 1 |
| 8 | Persistenz, Versionierung und Mandantenschutz | Connection Strings in Production verpflichtend machen; keinen stillen InMemory-Fallback zulassen. Eindeutige Schlüssel pro Tenant/Definition/Version, DB-seitige Tenant-Filter, Constraints und Autorisierungstests ergänzen. Data-Protection-Keyring persistent und podübergreifend speichern. | L | Muss | 4–6 |
| 9 | BPMN-Wait-States vervollständigen | Persistente Timer, Message-/Signal-Subscriptions, User-Task-Lifecycle, Boundary Events, Kompensation, Wiederanlauf und Incident-Recovery implementieren. Pro Semantik gezielte Trace-Assertions statt bloß `Count > 0` verwenden. | L | Muss | 5 |
| 10 | DMN und CMMN konsolidieren | Entweder den Produktumfang ehrlich als Subset deklarieren oder vollständige FEEL-/Hit-Policy-/DRD- und CMMN-Semantik implementieren. Doppelte DMN-Modelle entfernen. DMN-TCK und eine definierte CMMN-Conformance-Suite als CI-Gate integrieren. | L | Sollte | 2, 4–5 |
| 11 | Simulation, Migration und Management | Bis zur echten Implementierung Endpunkte mit HTTP 501 oder Feature Flag sperren. Migration als validierten, transaktionalen Job mit Preview, Token-Mapping, Rollback und Audit implementieren. Metriken aus Repositories/OpenTelemetry statt Konstanten erzeugen. | L | Sollte | 5, 8–9 |
| 12 | Observability und Process Mining | Persistent Mining Sink registrieren; Event und Zustandsänderung über Outbox koppeln. Echte Job-/Incident-/Queue-Metriken, Traces und korrelierbare IDs ergänzen. Readiness über `CanConnect`, Migrationstand und Broker prüfen; bei DB-Initialisierungsfehlern den Start abbrechen. | M | Muss | 5–6 |
| 13 | Produktionsdeployment | Ungültige Stage-Konfiguration reparieren. Kubernetes-Secrets, sämtliche Connection Strings, JWT, Shared Keyring, Port-Binding, Liveness/Readiness, Ressourcen, Security Context, PDB und migrationssicheren Deploymentablauf ergänzen. Keine Klartextpasswörter oder `latest`-Tags verwenden. | M | Muss | 6, 8, 12 |
| 14 | Studio- und API-Verträge härten | UI-Fehler sichtbar machen, Nullability-/MudBlazor-Warnungen beseitigen und Compliance-Aussagen entfernen, bis sie nachgewiesen sind. Engine-Test-Run muss auf einen echten Endzustand oder eine wartende Aktivität prüfen. | M | Sollte | 3–4, 9 |
| 15 | Release- und Security-Gates | Korrekte `.github/dependabot.yml`, NuGet/npm Audit, SBOM, Secret Scan, SAST, Container Scan, OpenAPI-Diff, Coverage- und Conformance-Gates einrichten. Releases nur aus sauberem Checkout und mit vollständigem API/Engine/Studio-End-to-End-Lauf erlauben. | M | Muss | 2–14 |

## Empfohlene Umsetzung in Phasen

### Phase 1: Lieferfähigkeit herstellen

- Punkte 1–3 abschließen.
- Sauberer Restore, Build und Testlauf unter Linux und Windows.
- Verbindlicher, zunächst roter End-to-End-Test für den BPMN-Kernpfad.

#### Umsetzungsstand 25.08.2026

| Punkt | Status | Nachweis / nächster Schritt |
| --- | --- | --- |
| 1. Produktumfang und Acceptance Matrix | abgeschlossen | `docs/reference/product-support-matrix.md` ist die verbindliche Matrix; README-Produktionsreifeaussagen wurden auf den belegten Umfang reduziert. |
| 2. Reproduzierbarer Build und Test-Runner | abgeschlossen | .NET SDK `10.0.302` ist exakt gepinnt, Microsoft Testing Platform ist der Runner und CI baut/testet Linux plus Windows mit sequenziellen Testmodulen. Versionierte `bin\\Debug`-Artefakte sind entfernt und werden durch ein Gate verhindert. Frische lokale Linux- und Windows-Kopien wurden vollständig restauriert, gebaut und getestet. |
| 3. End-to-End-Vertragstests | abgeschlossen | Sieben echte API-/SQLite-Verträge decken `P1-AC-01` bis `P1-AC-05` ab: Kernpfad, Timer Catch/Boundary, Message, Signal, Host-Restart und Parallel-Join mit zwei Instanzen. Die in Phase 1 zunächst rote Baseline ist durch Phase 2 vollständig grün; das CI-Skript akzeptiert ausschließlich erfolgreiche fachliche Endzustände und persistente Wait-States. |

Phase 1 ist damit **abgeschlossen**. Die damals bewusst rote Vertragsbaseline wurde in Phase 2 ohne Abschwächung in ein blockierendes Green-Gate überführt.

#### Verifikation vom 25.08.2026

| Plattform | Restore / Build | Grüne Suite | Phase-1-Vertragsgate |
| --- | --- | --- | --- |
| Linux, SDK `10.0.302` | sauberer Restore; Release-Build mit 0 Fehlern | siehe aktuelle Phase-2-Verifikation | 7/7 fachlich erfolgreich |
| Windows, SDK `10.0.303` per `latestPatch` | sauberer Restore; Release-Build mit 0 Fehlern | siehe aktuelle Phase-2-Verifikation | 7/7 fachlich erfolgreich |

Der erste externe GitHub-Actions-Matrixlauf folgt nach Commit/Push. Die lokale Prüfung verwendete getrennte, artefaktfreie Kopien; auf Windows wurde das exakt gepinnte SDK portabel eingebunden.

### Phase 2: Produktionsfähigen BPMN-Kern bauen

- Punkte 4–9 umsetzen.
- Deploy, Start, Wait, Resume, Complete und Restart dauerhaft ausführbar machen.
- Sämtliche Fake-, No-op- und In-Memory-Abhängigkeiten aus dem Produktionsprofil entfernen.

#### Umsetzungsstand 25.08.2026

| Punkt | Status | Nachweis / Grenze |
| --- | --- | --- |
| 4. Einheitlicher ausführbarer Runtime-Pfad | abgeschlossen | `IProcessExecutionRuntime` kapselt Deployment, Start, Resume, Korrelation, Jobs und Recovery; öffentliche Runtime-/Task-/Incident-APIs verwenden die persistente Implementierung. |
| 5. Dauerhafte Zustandsmaschine | abgeschlossen | EF-Modelle und Migrationen umfassen Tokens, Variablen, Subscriptions, Tasks, Jobs, Worker, Incidents, Inbox und Outbox; Revisionen, eindeutige Idempotency-Claims und Job-Leases sichern Konkurrenzzugriffe. |
| 6. Produktions-DI bereinigen | abgeschlossen für den Phase-2-Kern | Production/Stage verwerfen Fake-, InMemory- und NoOp-Abhängigkeiten. Persistenter Mining Sink, Worker Store und durable Outbox-Dispatcher sind registriert. Externe Brokerzustellung und Broker-Readiness gehören zu Phase 3. |
| 7. Script-, Connector- und Plug-in-Isolation | abgeschlossen | In-Process-Scripts sind in Production/Stage verboten; Connector-Ziele besitzen getrennte Allowlisten und Private-/Link-Local-Schutz; Plug-ins benötigen Datei-Allowlist und SHA-256-Prüfsumme. |
| 8. Persistenz, Versionierung und Mandantenschutz | abgeschlossen | Connection Strings und Data-Protection-Keyring sind in Production/Stage verpflichtend; Definitionen sind tenantbezogen versioniert; positive und negative Tenant-Verträge sind grün. |
| 9. BPMN-Wait-States vervollständigen | abgeschlossen für den deklarierten Subset | User Tasks, Timer Catch/interrupting Boundary, Message, Signal, Parallel Join, begrenzte Kompensation, Host-Restart und Incident-Recovery sind persistent und end-to-end getestet. Nicht unterstützte Standardsemantik bleibt in der Supportmatrix ausdrücklich begrenzt. |

Phase 2 ist damit für den in `docs/reference/product-support-matrix.md` definierten BPMN-Subset **abgeschlossen**. Dies ist keine Behauptung vollständiger BPMN-2.0-Konformität und keine Freigabe der gesamten Plattform; externe Outbox-Zustellung, Readiness/Failover und Deploymenthärtung folgen in Phase 3.

#### Verifikation vom 25.08.2026

| Prüfung | Linux | Windows |
| --- | --- | --- |
| Sauberer Restore und Release-Build | erfolgreich, 0 Fehler | erfolgreich, 0 Fehler |
| Reguläre Suite ohne separat ausgeführtes Phase-1-Gate | 677 gesamt: 676 erfolgreich, 1 bewusst übersprungen, 0 fehlgeschlagen | 677 gesamt: 676 erfolgreich, 1 bewusst übersprungen, 0 fehlgeschlagen |
| Phase-1-Regressionsgate | 7/7 erfolgreich | 7/7 erfolgreich |
| Phase-2-Acceptance-Verträge | 6/6 gezielt erfolgreich; zusätzlich Bestandteil der regulären Suite | Bestandteil der vollständig erfolgreichen regulären Suite |
| Produktionskonfiguration | 5/5 gezielt erfolgreich; zusätzlich Bestandteil der regulären Suite | Bestandteil der vollständig erfolgreichen regulären Suite |
| EF-Core-Migrationen | Keine ausstehenden Modelländerungen; alle 15 Migrationen auf einer frischen SQLite-Datenbank erfolgreich angewendet | nicht separat angewendet; Windows-Build und -Tests verwenden dasselbe EF-Modell und sind grün |

Der einzige übersprungene Test ist der bereits bestehende echte OpenAI-Integrationstest, der ohne `OPENAI_API_KEY` absichtlich nicht ausgeführt wird. Build-Warnungen aus dem Bestand bleiben sichtbar; Phase 2 fügt keine Fehlerunterdrückung hinzu.

### Phase 3: Betriebssicherheit herstellen

- Punkte 12–13 umsetzen.
- Health, Telemetrie, Datenbankmigrationen, Secrets und Mehr-Pod-Betrieb real testen.
- Recovery- und Failover-Tests in CI integrieren.

#### Umsetzungsstand Phase 3 (2026-08-26)

Die Implementierung der Punkte 12–13 ist auf dem Phase-3-Branch abgeschlossen:

- Der persistente Runtime-Outbox-Publisher least Nachrichten atomar und replika-sicher, verwendet stabile Message-IDs, Retry-/Dead-Letter-Semantik und RabbitMQ-Publisher-Confirmations beziehungsweise einen idempotenten Kafka-Producer.
- Readiness prüft Verbindung und Migrationsstand aller fünf Engine-Kontexte, der Dependency-Registry sowie den externen Broker. Normale Produktions-Pods wenden keine Migrationen an; ein separater `--migrate-only`-Job wird vor dem Deployment ausgeführt.
- Management- und Prometheus-Ausgaben lesen echte persistente Prozess-, Job-, Incident-, Subscription-, Worker- und Outbox-Zähler. Outbox-Traces, Metriken und validierte `X-Correlation-ID`-Weitergabe sind in OpenTelemetry integriert.
- Die Kubernetes-Ressourcen sind in Voraussetzungen, versionierten Migrations-Job und API-Deployment getrennt. Secrets, gemeinsamer persistenter State/Key-Ring, Port 8080, Startup-/Live-/Ready-Probes, Ressourcenlimits, Non-Root-/Read-Only-Security-Context, PDB und unveränderliche Image-Version sind definiert. Das geordnete Rollout sowie Recovery sind im Produktions-Runbook beschrieben.
- Der CI-Job `operational-integration` verwendet echte RabbitMQ- und PostgreSQL-Dienste. Er prüft Broker-Roundtrip, die Ablehnung unroutbarer Nachrichten, alle EF-Migrationen und konkurrierendes Outbox-Leasing durch zwei isolierte Service-Provider auf derselben PostgreSQL-Datenbank. Kubernetes-Manifeste werden zusätzlich per Client-Dry-Run validiert.

Lokale Nachweise: Release-Build erfolgreich; Phase-3-Akzeptanztests 9/9 einschließlich providerneutraler PostgreSQL-/SQL-Server-Migrationsskripte; reguläres Haupttestprojekt 666 erfolgreich und ein bestehender OpenAI-Test ohne API-Key übersprungen; separates Phase-1-Gate 7/7; fünf Engine-Modelle plus Dependency-Registry ohne ausstehende Modelländerungen; OpenAPI-Snapshot aktuell. Der externe RabbitMQ-/PostgreSQL- und Kubernetes-Nachweis kann lokal ohne Docker/Kubernetes nicht ausgeführt werden und wird erst mit dem CI-Lauf des gepushten Branches endgültig bestätigt.

### Phase 4: Erweiterte Features qualifizieren

- Punkte 10–11 und 14 umsetzen.
- DMN, CMMN, Simulation und Migration nur nach bestandenen End-to-End- und Conformance-Tests freigeben.

#### Umsetzungsstand Phase 4 (2026-08-26)

- Der öffentliche DMN-Vertrag ist auf genau eine Decision Table, einfache Gleichheitsbedingungen und die nachgewiesenen Hit Policies `UNIQUE`, `FIRST`, `ANY`, `COLLECT` und `RULE ORDER` begrenzt. XML wird mit deaktivierter DTD-/Resolver-Verarbeitung geparst; unvollständige Modelle und nicht unterstützte Hit Policies werden beim Deployment abgelehnt. Nicht verwendete doppelte DMN-Entitätsmodelle wurden entfernt.
- CMMN bleibt auf persistentes Deployment und Lesen von Definitionen beschränkt. Case-Ausführung antwortet standardmäßig mit HTTP 501, CMMN-gRPC-Operationen mit `Unimplemented`, und die Capability-Antwort bewirbt keine CMMN-Ausführung.
- Simulation, Simulation Analytics und beide Prozessmigrations-APIs antworten fail-closed mit HTTP 501. Ihre vorhandenen Platzhalter- beziehungsweise nicht transaktional qualifizierten Dienste sind damit im öffentlichen Standardprofil nicht erreichbar. Production und Stage verweigern den Start, falls einer dieser Ausführungs-Flags aktiviert wird.
- Das Studio zeigt Konfigurationsfehler sichtbar an, enthält keine unbelegten Compliance-Zusicherungen mehr und der Engine-Test-Run akzeptiert nur einen nachgewiesenen Endzustand oder einen persistenten Wait-State. Die identifizierten Nullability- und MudBlazor-Komponentenwarnungen wurden beseitigt.
- Das CI-Gate `scripts/verify-phase4-acceptance.sh` prüft neun konkrete End-to-End-Fälle: den freigegebenen DMN-Subset, die explizite Ablehnung außerhalb des Subsets sowie die fail-closed Grenzen für CMMN-REST/gRPC, Simulation und Migration. Eine vollständige offizielle DMN-TCK oder CMMN-Semantik wird ausdrücklich nicht behauptet und bleibt außerhalb des freigegebenen Produktumfangs.

Phase 4 ist damit für den in `docs/reference/product-support-matrix.md` definierten Umfang **abgeschlossen**. Die Gesamtplattform bleibt bis zu den Release- und Security-Gates aus Phase 5 nicht vollständig produktionsreif.

Lokale Nachweise: vollständiger Release-Build erfolgreich mit 0 Fehlern; reguläre Solution-Suite 694 erfolgreich, 1 bestehender OpenAI-Test ohne API-Key übersprungen und 0 fehlgeschlagen; separates persistentes BPMN-Gate 7/7; Phase-4-Gate 9/9; Studio-UI-Verträge 21/21; OpenAPI-Snapshot aktuell; keine versionierten `bin`-/`obj`-Artefakte. Die verbleibenden Warnungen des finalen inkrementellen Builds sind 15 `NU1900`-Hinweise, weil der lokale Sandbox-Lauf die NuGet-Vulnerability-Quelle nicht erreichen konnte.

### Phase 5: Release absichern

- Punkt 15 vollständig abschließen.
- Reproduzierbares Release aus einem sauberen Checkout erzeugen.

#### Umsetzungsstand Phase 5 (2026-08-26)

Punkt 15 ist für den definierten ersten Produktionsmeilenstein umgesetzt:

- Dependabot überwacht NuGet, Studio-npm und GitHub Actions. Pull Requests erhalten zusätzlich ein blockierendes Dependency-Review-Gate; aufgelöste NuGet- und npm-Abhängigkeiten werden separat auditiert.
- CodeQL analysiert C# und JavaScript/TypeScript. Trivy blockiert hohe/kritische Dependency-, Secret-, Misconfiguration- und Containerbefunde. Für die tatsächlich gebauten API- und Studio-Images werden getrennte SPDX-JSON-SBOMs erzeugt.
- Das Coverage-Gate misst über Microsoft Testing Platform mindestens 60% Zeilen- und 45% Branch-Coverage. OpenAPI-Snapshot, persistente BPMN-Verträge und die Phase-4-Qualifikationsverträge bleiben unverändert blockierend.
- Tag-Releases warten auf Linux-/Windows-Build, Audits, beide CodeQL-Läufe, Supply-Chain-Gates und den echten RabbitMQ-/PostgreSQL-Lauf. Ein separater sauberer Checkout baut und testet API, Engine und Studio erneut und muss danach unverändert sein.
- SDK und CLI werden zweimal gebaut. Variable NuGet-Container-Metadaten werden kanonisiert; nur byteidentische Pakete mit geprüfter `SHA256SUMS`-Datei werden weitergereicht. GitHub attestiert deren Provenance, bevor NuGet Trusted Publishing per OIDC veröffentlicht.
- Das Studio-Containerfile verwendet gepinnte .NET-Images, den korrekten Repository-Buildkontext und einen Non-Root-Runtime-User. `.dockerignore`, `SECURITY.md` und das Security-/Release-Runbook dokumentieren Angriffsfläche, Meldeweg und Betreiberpflichten.
- Ein echter Installations-Smoke-Test des CLI-Pakets deckte einen Datenbankzugriff bei `--help` und eine kollidierende Default-Datei auf. Hilfe wird nun vor Host-/Persistenzinitialisierung ausgegeben; die Dependency-Registry verwendet standardmäßig `vertexbpmn-dependencies.db`.

Phase 5 ist damit im Repository **abgeschlossen**. Die erstmalige externe Ausführung der neuen CodeQL-, Trivy-, SBOM-, Container- und Provenance-Schritte sowie die Konfiguration der erforderlichen Branch-Protection-Checks sind nach Commit/Push in GitHub zu bestätigen; sie können lokal nicht als GitHub-Plattformzustand vorweggenommen werden.

Lokale Nachweise vom 26.08.2026: Release-Build mit 0 Fehlern; reguläre Solution-Suite 698 gesamt, 697 erfolgreich, 1 bestehender OpenAI-Test ohne API-Key übersprungen; Coverage-Suite 672 gesamt mit 60,95% Zeilen- und 45,83% Branch-Coverage; Phase-1-Gate nach Korrektur der SQLite-Mehrverbindungsnutzung zweimal in Folge 7/7; Phase-4-Gate 9/9; OpenAPI-Snapshot aktuell; NuGet-Audit 0 vulnerable Einträge; npm-Audit 0 hohe/kritische Befunde; Studio-Moddle-Roundtrip grün; SDK und CLI aus zwei Läufen byteidentisch und per SHA-256 bestätigt; das erzeugte CLI-Paket erfolgreich als .NET Tool installiert und `--help` ohne Persistenzzugriff ausgeführt; Workflow mit actionlint 1.7.12 ohne Befund validiert; keine versionierten Build-Artefakte. Die lokale WSLC-Runtime 2.9.3.0 baute API (`sha256:a436d803d2ea5a9d6bff181c6ff9708da564593f6cfe11999cc6a24325ba5758`) und Studio (`sha256:fc527fccf145f23d6527e44560ab201c3dd3ab7084b114ba604618b9cf4bae4e`) erfolgreich; kurzlebige Container-Smoke-Tests bestätigten für beide Images die Anwendungs-DLL, .NET-/ASP.NET-Runtime 10.0.11 und tatsächliche Non-Root-UID 1654. Trivy-Scans, SBOM-Erzeugung und GitHub-Provenance bleiben bis zum ersten GitHub-Actions-Lauf extern zu bestätigen.

## Definition of Done für den ersten Produktionsmeilenstein

- Ein BPMN-Modell kann über die öffentliche API validiert, versioniert, deployt und tatsächlich ausgeführt werden.
- Service Tasks, User Tasks, Timer sowie Message- und Signal-Wait-States überleben Prozess- und Pod-Neustarts.
- Zwei oder mehr API-/Worker-Replikate arbeiten auf einem konsistenten Zustand ohne doppelte Ausführung.
- Fehler erzeugen persistente Incidents und nachvollziehbare Retry-/Dead-Letter-Einträge.
- Kein Fake-, No-op- oder In-Memory-Service ist im Produktionsprofil aktiv.
- Untrusted C#-Code wird nicht im API-/Engine-Prozess ausgeführt.
- Readiness schlägt bei nicht erreichbarer Datenbank, fehlender Migration oder nicht verfügbarem Broker fehl.
- Mandantenisolation ist durch DB-Constraints, Autorisierung und negative Integrationstests belegt.
- Der vollständige Build, alle End-to-End-Tests, Audits und Security-Gates laufen in einem sauberen CI-Checkout erfolgreich.
- README, OpenAPI und Studio zeigen ausschließlich nachweislich unterstützte Funktionen.
