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

### Phase 2: Produktionsfähigen BPMN-Kern bauen

- Punkte 4–9 umsetzen.
- Deploy, Start, Wait, Resume, Complete und Restart dauerhaft ausführbar machen.
- Sämtliche Fake-, No-op- und In-Memory-Abhängigkeiten aus dem Produktionsprofil entfernen.

### Phase 3: Betriebssicherheit herstellen

- Punkte 12–13 umsetzen.
- Health, Telemetrie, Datenbankmigrationen, Secrets und Mehr-Pod-Betrieb real testen.
- Recovery- und Failover-Tests in CI integrieren.

### Phase 4: Erweiterte Features qualifizieren

- Punkte 10–11 und 14 umsetzen.
- DMN, CMMN, Simulation und Migration nur nach bestandenen End-to-End- und Conformance-Tests freigeben.

### Phase 5: Release absichern

- Punkt 15 vollständig abschließen.
- Reproduzierbares Release aus einem sauberen Checkout erzeugen.

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
