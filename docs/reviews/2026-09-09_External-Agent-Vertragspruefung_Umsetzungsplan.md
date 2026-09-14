# External Tasks und Agent-Vertragsprüfung – Umsetzungsplan

Stand: 2026-09-12. Ursprüngliche Planungsbasis: `5b211c6a897c2e78a7518f5a00a2e201acef5d1f`; A00 geprüft gegen `9f65335a45e61b50d421b05850e0e8115dc69d65`.
Status: **A00 bis A03 abgeschlossen.** Aktueller Stand vom 2026-09-14: [A03-Prüfbericht mit realem PostgreSQL-Race-, Lease-, JWT- und Worker-Nachweis](2026-09-14_External-Agent_A03_Pruefbericht.md). A03 stellt Claim/Heartbeat und das sichere Worker-Grundgerüst bereit; Complete/Fail, atomare BPMN-Fortsetzung und Crash-Recovery bleiben ausdrücklich A04. Das Feature bleibt bis dahin standardmäßig deaktiviert und ist nicht produktionsfreigegeben. Ergebnisse der Vorpakete: [A02-Prüfbericht](2026-09-13_External-Agent_A02_Pruefbericht.md), [Inventur](2026-09-12_External-Agent-A00_Inventur.md), [verbindlicher Vertrag v1](2026-09-12_External-Agent-A01_Vertrag.md), [Architektur-/Security-Selbstreview](2026-09-12_External-Agent-A01_Review.md). Bei abweichenden Vorschlägen in Abschnitt 4 gilt die Konkretisierung aus A01.

## 1. Auftrag und erster Anwendungsfall

Ein wiederverwendbarer persistenter External-Service-Task ermöglicht lange externe Verarbeitung, ohne einen Engine-Thread oder eine Datenbanktransaktion während der Arbeit offen zu halten. Als erster Worker wird eine KI-gestützte Vertragsvorprüfung umgesetzt.

**Planungsannahme:** Der Nutzer hat noch keinen konkreten fachlichen Fall festgelegt. Dieser Plan übernimmt deshalb `contract-reviewer` aus dem [ursprünglichen Agent-Vorschlag](2026-09-09_Agent_Task_Implementierungsplan.md). Vor A05 bestätigen oder durch einen anderen read-only Dokumentanalysefall ersetzen. Das Originaldokument bleibt unverändert; dessen Code ist eine Skizze, kein Implementierungsvertrag.

Fachlicher Ablauf:

1. Ein berechtigter Benutzer startet einen Prozess mit Referenz auf einen tenantgebundenen, unveränderlichen Vertragstext.
2. Der Agent identifiziert vereinbarte Klauselkategorien, liefert Fundstellen und kennzeichnet Unsicherheiten.
3. Die Engine übernimmt ausschließlich ein technisch validiertes Ergebnis.
4. Ein menschlicher User Task prüft das Ergebnis und trifft die Entscheidung.
5. Ein endgültiger technischer/fachlicher Fehler oder Timeout führt in einen modellierten manuellen Prüfpfad.

**Kein automatisches Vertragsurteil:** Das Ergebnis ist eine vorbereitende Analyse, keine Rechtsberatung oder Freigabe. Keine Unterschrift, E-Mail, Zahlung, Vertragsänderung oder sonstige externe Schreibaktion. Ein formal gültiges JSON-Ergebnis ist noch kein fachlich richtiges Ergebnis.

Erster Umfang: Textdokumente mit festen Abschnitts-IDs; keine PDF-/OCR-Pipeline, kein Webzugriff, keine Multi-Agent-Delegation, kein beliebiger MCP-Server und keine frei ausführbaren Skripte.

## 2. Bestehende Grundlagen und Integrationsstellen

Pfade sind repository-relativ. Vor jedem Paket aktuellen Checkout prüfen und geänderte Grundlagen dokumentieren; keine Dateien aus früheren Ständen blind ersetzen.

| Grundlage | Vorhandener Einstieg | Verwendung |
|---|---|---|
| Parser und XML-Konvention | `src/VertexBPMN.Engine/Parsing/{BpmnParser,VertexBpmnExtensions}.cs` | Vorhandenen Namespace `https://vertexbpmn.io/schema/bpmn/1.0` verwenden; nicht den abweichenden `.dev`-Namespace aus der Skizze. |
| Fachliches BPMN-Modell | `src/VertexBPMN.Domain/Model/Bpmn/BpmnModel.cs` | Bestehendes `BpmnTask` erweitern; keinen fiktiven `ProcessNode` übernehmen. |
| Persistente Ausführung | `src/VertexBPMN.Engine/Execution/PersistentProcessExecutionRuntime.cs` | Service-Task-Pfad unterscheidet abgeschlossen/wartend; internes `ExecutionNode` berücksichtigen. |
| Tokens | `src/VertexBPMN.Domain/Entities/ExecutionToken.cs` | Vorhandene State-/Revision-Konventionen nutzen; Aktivitätsdurchlauf und Scope-Zuordnung ergänzen, falls nötig. |
| Leasing | `src/VertexBPMN.Infrastructure/Persistence/Repositories/JobRepository.cs` | Bedingte Updates und Revisionen als Muster prüfen, nicht Timer-Jobs ungeprüft als Agent-Jobs verwenden. |
| Persistenz und Outbox | `src/VertexBPMN.Infrastructure/Persistence/BpmnDbContext.cs`, `src/VertexBPMN.Domain/Entities/RuntimeOutboxMessage.cs` | Transaktionsgrenzen und Zustellpfad tatsächlich prüfen, bevor Wiederverwendung beschlossen wird. |
| AI-Altpfad | `src/VertexBPMN.Application/Handlers/GenericAiServiceTaskHandler.cs` | Im vorherigen Review als Mock identifiziert; erneut verifizieren. Nicht als echte Runtime oder Fallback für neue Agent-Jobs verwenden. |
| Studio | `src/VertexBPMN.Studio/tools/bpmn-io/src/{vertex.json,vertex-properties-provider.js,vertex-validation.js}` und bestehender BPMN-Katalog | Agent als Service-Task-Preset integrieren, keinen zweiten Editor bauen. |

Neue vorgeschlagene Typen: `ExternalTaskJob`, `ExternalTaskAttempt`, `ExternalTaskDefinition`, `IExternalTaskService`, `IAgentRuntime`, `AgentExecutionPolicy`. Das sind Zielnamen, keine Behauptung bereits existierender Klassen. Gemeinsame Definitionen gehören in Domain, Orchestrierung in Application, EF/Provider in Infrastructure; keine Domain-Abhängigkeit auf Application einführen.

## 3. Nicht verhandelbare Grenzen

- Keine LLM-/Tool-/Broker-Netzwerkaufrufe innerhalb einer Engine-DB-Transaktion.
- Job, Aktivitäts-Wait und initiale Historie gemeinsam committen. Wiederholte Engine-Schritte erzeugen keinen zweiten Job für denselben Durchlauf.
- Queue-Ausführung ist mindestens-einmal. Ein Ergebnis darf den lokalen Runtime-Zustand nur einmal wirksam verändern; keine allgemeine Exactly-once-Zusage für externe Nebenwirkungen.
- Tenant und Worker-Rechte aus authentifizierter Identität ableiten. Body-`WorkerId`, Modellkonfiguration und Tool-Antworten verleihen keine Berechtigungen.
- Jede Übernahme erhält eine neue Lease-ID/Fencing-Generation; alte Worker verlieren Heartbeat-/Complete-/Fail-Rechte.
- Engine-Abbruch, interrupting Boundary Event und endgültige Deadline schlagen verspätete Completion. Nicht-interrupting Events beenden den Job nicht pauschal.
- Modellausgabe und Dokumentinhalt sind untrusted data, keine Anweisungen zur Erweiterung von Toolrechten oder zum Ändern des Prozesses.
- Nur explizit gemappte Eingaben bereitstellen, niemals alle Prozessvariablen oder Credential-Secrets serialisieren.
- GUI-Tests ausschließlich lokal. Keine neue Docker-Pflicht; WSLC oder vorhandene isolierte Dienste verwenden. Benutzerinstanzen nicht verändern.
- Bestehende Security-Gates, SSRF-Grenzen, Namespace-/Roundtrip-Regeln und Engine-Semantik nicht zur Vereinfachung abschwächen.

## 4. Verträge vor Code

### 4.1 BPMN und Konfiguration

Vorgeschlagene Repräsentation: normaler `bpmn:serviceTask` mit `vertex:externalTask` in `extensionElements`. `topic="agent.contract-review"` wählt den Worker-Vertrag. Ein freigegebenes `agentProfileRef` verweist auf die Agentkonfiguration; allgemeine Tasks müssen keine LLM-Felder kennen. Endgültige XML-Namen in A01 festlegen und mit Moddle/Parser/Serializer gemeinsam testen.

Konfiguration: Input-Mapping, Output-Mapping, Topic, Profilreferenz, maximal erlaubte Versuche und Gesamtdeadline. Profil enthält versionierte Promptvorlage, Modellpolicy, Ergebnisschema, Tool-Allowlist und Ressourcenbudgets. Definition/Profil/Schema werden pro Job unveränderlich referenziert oder als sicherer Snapshot festgehalten. Policy-Widerruf kann dennoch laufende Ausführungen sperren.

`maxRetries=0` ist gültig; `maxAttempts = maxRetries + 1`. Strenge Obergrenzen und numerische Überlaufprüfung. Keine frei wählbare Provider-URL oder Credentials im BPMN.

### 4.2 Persistenz und Lebenszyklus

Vorgeschlagene Zustände: `Ready → Leased → Completed`; `Leased → RetryScheduled → Ready`; terminal `Failed`, `Cancelled`, `TimedOut`. Zustandsmatrix in A01 vollständig definieren, einschließlich Recovery und idempotenter Wiederholung.

Mindestens speichern:

- Job-ID, Tenant, Prozess-/Definitions-ID, Element, Scope und **ActivityExecutionId** für den konkreten Durchlauf.
- Eingangssnapshot beziehungsweise autorisierte Dokumentversion mit Inhalts-Hash; Profil-/Schema-Version.
- Status, Revision, AvailableAt, absolute Deadline, Versuchszähler und Versuchslimit.
- Worker-Subject, Lease-ID/Generation und Ablauf; monotone Konkurrenzkontrolle.
- Validiertes Ergebnis, redigierter Fehlercode, Zeitstempel und protokollierte Versuche.
- Completion-ID und Ergebnis-Hash für berechtigte idempotente Wiederholung.

Unique-Key auf die konkrete Aktivitätsausführung, nicht nur Element-ID. Schleifen und Multi-Instance müssen neue legitime Jobs erzeugen können. Tenant und durchlaufbezogene Referenzen serverseitig konsistent validieren.

UTC-Typen und JSON-Speicherung providerabhängig konfigurieren: PostgreSQL-Produktionsprofil und weiterhin zugesagte SQLite-Verwendung berücksichtigen. Kein pauschales `jsonb`, keine erneute ungeprüfte DateTimeOffset-Abfrage. Migrationen für Neuinstallation und Upgrade erforderlich.

### 4.3 Worker-Protokoll

Vorgeschlagene API-Routen unter `/api/external-tasks`; endgültige Routen/DTOs in A01 festlegen:

- `claim`: begrenzte Batchgröße, autorisierte Topics/Tenants, serverbegrenzte Lease-Dauer. Auswahl und Update müssen Tenant, Status, AvailableAt, Deadline, Attempts und Konkurrenzbedingungen prüfen.
- `heartbeat`: gültige aktuelle Lease verlängern, höchstens bis zur absoluten Deadline.
- `complete`: aktuelle Identität, Tenant, Lease, wartende Aktivität und Output-Schema prüfen; Ergebnis atomar annehmen.
- `fail`: technische Retryklasse oder erlaubten fachlichen Fehler melden; Retryentscheidung trifft der Server, nicht allein der Worker.
- Status/Versuchshistorie: berechtigte, redigierte Betriebsansicht; keine allgemeinen Prompt-/Dokumentdownloads.

Completion-Wiederholung nur unter derselben Berechtigung und mit derselben Completion-ID/Ergebnisidentität akzeptieren. Abweichender Payload ist ein Konflikt. Ein fremder Aufrufer erhält auch bei bereits abgeschlossenem Job keinen unautorisierten Erfolg.

Eine Completion quittiert erst einen dauerhaft gesicherten Zustand. Bevorzugt Ergebnis und Pending-Fortsetzung atomar speichern; nachfolgende Ausführung separat. Bei Outbox-basierter Fortsetzung deren Empfänger und Deduplication tatsächlich implementieren. Nur einen Outbox-Datensatz anzulegen ist keine funktionierende Wiederaufnahme.

### 4.4 Agent-Ein-/Ausgabe

Eingabe: `documentId`, unveränderliche `documentVersion`, Sprache, freigegebenes Prüfprofil. Im ersten Umfang werden nur synthetische/anonymisierte Vertragstexte verwendet. Dokumentzugriff ist tenantgebunden und auf den Job beschränkt.

Vorgeschlagenes Ergebnis:

```json
{
  "schemaVersion": "contract-review.v1",
  "documentVersion": "immutable-version-id",
  "summary": "Kurze Zusammenfassung",
  "findings": [
    {
      "category": "termination",
      "severity": "review",
      "sectionId": "section-12",
      "quote": "Exakter kurzer Beleg aus diesem Abschnitt",
      "explanation": "Warum ein Mensch diese Stelle prüfen sollte"
    }
  ],
  "uncertainties": [],
  "requiresHumanReview": true
}
```

A05 legt Kategorien, Längen-/Anzahllimits und zulässige Werte fest. Schema-valides Ergebnis zusätzlich auf passende Dokumentversion, existierende Abschnitts-IDs und tatsächlich vorkommende Belegzitate prüfen. `requiresHumanReview` wird serverseitig erzwungen; keine Freigabe durch Modelloutput. Keine freien Toolbefehle oder beliebigen Prozessvariablennamen im Ergebnis.

## 5. Arbeitspakete

Aufwand: relative Planungsgröße S ≈ 1–2, M ≈ 3–5, L ≈ 6–10 Entwicklertage inklusive Tests; keine garantierte Modelllaufzeit. Nach A00 neu schätzen. Nicht alle Pakete parallel beginnen.

| ID | Ergebnis | Voraussetzung | Aufwand |
|---|---|---|---|
| A00 | Inventur und reproduzierbare Baseline | keine | M |
| A01 | Verbindlicher External-Task-/Security-Vertrag | A00 | M |
| A02 | Atomare Job-Erzeugung und persistenter Wait | A01 | L |
| A03 | Sichere Lease-API und Worker-Grundgerüst | A02 | L |
| A04 | Completion, Fehler, Abbruch und Recovery | A03 | L |
| A05 | Read-only Vertragsprüfer mit echter Runtime | A01; Integration nach A04 | L |
| A06 | Studio-Preset und Betriebssicht | A01; Integration nach A04/A05 | M |
| A07 | Reale lokale Gesamt- und Fachabnahme | A02–A06 | L |

### A00 – Vorhandene Mechanismen prüfen

- [x] Referenzierten Code und aktuelle Produktions-/Studio-Pläne lesen; Änderungen seit Planungsbasis erfassen.
- [x] Alle angebotenen Engine-Ausführungsmodi, Wait-/Boundary-/Scope-/Multi-Instance-Pfade sowie aktuelle API-Policies inventarisieren.
- [x] Outbox-/Inbox-, Leasing-, Job-Recovery-, Runtime-Transaktions- und AI-Pfade prüfen; wiederverwendbare Bausteine mit Codeverweisen nennen.
- [x] Bestehende Tests lokal ausführen und neue Kontrollpunkte für Crash-/Race-Tests identifizieren.

Abnahme: **Erfüllt und dokumentiert** in [A00 Inventur und Baseline](2026-09-12_External-Agent-A00_Inventur.md). Für Simple und Legacy-Distributed ist bis zu einer vollständigen Implementierung eine explizite Ablehnung festgelegt; Zielmodus ist die persistente Runtime.

### A01 – Verträge und Sicherheitsreview

- [x] Abschnitt 4 konkretisieren: XML/DTOs, Zustandsmatrix, atomare Grenzen, Fehlercodes, Attempts/Deadline, Aktivitätsidentität und unterstützte Engine-Modi.
- [x] Integration der Worker-Authentifizierung in vorhandenes Auth-System entwerfen; eigene eingeschränkte Worker-Policy sowie Tenant-/Topic-/Profilberechtigungen festlegen. Laufzeitimplementierung und Handler-Negativtests folgen in A03.
- [x] Geheimnis-/Dokumentzugriff, Audit und Aufbewahrungsvertrag festlegen. Pilotfristen sind Vorschläge; konkrete Produktfristen bleiben eine explizite A05-Entscheidung und Voraussetzung der Produktionsaktivierung.
- [x] Serverzeit/TimeProvider, CAS-/Lease-Verfahren, Completion-Deduplication und Datenbankmigration entwerfen.
- [x] Bei lokalem Modellserver: operatorseitig festgelegte Endpunkt-Allowlist und Egress-Policy. Private lokale Ziele nur im getrennten Workerprofil erlauben, nicht durch Lockerung des allgemeinen Connector-SSRF-Schutzes.

Abnahme: prüfbare Vertragsbeispiele plus separates Architektur-/Security-Review vor Umsetzung. Unentschiedene Produktfragen konkret vorlegen; keine Infrastruktur oder externen AI-Zugänge stillschweigend voraussetzen.

Ergebnis 2026-09-12: [Vertrag v1](2026-09-12_External-Agent-A01_Vertrag.md) und [separates Selbstreview](2026-09-12_External-Agent-A01_Review.md). A01 ist als Entwurf abgeschlossen; keine unabhängige Prüfung oder Laufzeitabnahme behauptet. Offene A05-Entscheidungen sind in beiden Dokumenten benannt.

### A02 – Domain, Migration, Parser und Wait

Zwischenstand 2026-09-12: **In Arbeit, nicht abgenommen.** `ExternalTaskDefinition` mit geprüften Limits und typisiertem Zugriff auf `BpmnTask` implementiert. XML-Prüfung erkennt Namespace, Owner, Duplikate, Konflikte und unbekannte Konfiguration. Normalisierter Parser-/Serializer-Roundtrip ist getestet. Simple/Legacy-Distributed besitzen eine Ablehnung am Modelleinstieg; die persistente Runtime bleibt für diesen Typ bis zur Job-/Wait-Implementierung gesperrt. Streaming lehnt den Typ ausdrücklich ab, damit seine vereinfachte Extension-Verarbeitung ihn nicht still verliert.

Verifikation: frischer Release-Build von `tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj` erfolgreich (0 Fehler, 31 bestehende Warnungen im letzten Lauf). Wegen wiederkehrender Sandbox-Schreibfehler im CLI-obj-Verzeichnis außerhalb der Sandbox wiederholt. Danach `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskDefinitionTests' -class '*UnifiedParserSerializerTests' -class '*StrictSerializerRoundtripTests' -class '*PersistentRuntimePhase2AcceptanceTests' -parallelMode none`: **22 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 11,883 s. Enthält 12 neue External-Task-Fälle.

Fortsetzung 2026-09-12: `ExternalTaskJob`, `ExternalTaskAttempt`, `ExternalTaskContinuation`, nullable Aktivitäts-/Scope-IDs am Token und EF-Mapping implementiert. Migration `20260912090238_ExternalTaskPersistence` samt Snapshot/Designer angelegt; PostgreSQL-GUIDs/Zeitwerte explizit als uuid/bigint, SQLite entsprechend TEXT/INTEGER. Downgrade verhindert Entfernung bei vorhandenen Jobs. SQLite-Downgrade verwendet direkte Spaltenentfernung, weil ältere Migrationen keine ausreichenden Zielmodelle für den EF-Rebuild liefern.

`ExternalTaskSchedulingStore.StageAsync` verlangt eine bestehende relationale Transaktion, prüft Tenant/Prozess/Definition/Wait und legt Job + Wait + redigierte Historie gemeinsam in deren Unit of Work an. Die Prozessrevision wird für konkurrierende Änderungen erhöht. Identische Wiederholung innerhalb desselben Durchlaufs legt keine zweite Zeile an. Der Baustein ist noch nicht in den öffentlichen Runtime-Service-Task-Pfad eingebunden; Inputs/Schema/Topic müssen vorher serverseitig validiert werden. Mehrreplikat-Konflikterholung ist weiterhin offen.

Aktueller Nachweis: frischer Testprojekt-Release-Build erfolgreich (0 Fehler, 31 bestehende Warnungen); `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -parallelMode none`: **16 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 2,429 s. Neue relationale SQLite-Tests prüfen Commit/Rollback für Job/Wait/Historie, Transaktionspflicht, Fremdtenant-Abweisung und Upgrade/Downgrade mit Erhalt bestehender Benutzer. PostgreSQL-SQL-Generierung prüft uuid/bigint ohne Verbindung; dies ist ausdrücklich keine reale PostgreSQL-Abnahme.

Weitere Fortsetzung 2026-09-12: Die persistente Runtime bindet den Store nun über `CreateExternalTaskWaitAsync` ein. Aktivierung ausschließlich mit `ExternalTasks:EnableSchedulingPreview=true` und explizit injiziertem `IExternalTaskContractResolver`; keine Standardregistrierung und keine Produktionsfreigabe. Der Resolververtrag verlangt lokale Tenant-/Topic-/Profil-/Schema-/Secret-Prüfung ohne Netzwerkzugriff. Ein echter Produktionsresolver fehlt noch. Variable Referenzen werden aus lokalem Scope beziehungsweise Prozessvariablen explizit gemappt, das Eingabesnapshot auf 128 KiB begrenzt; andere Ausdrücke werden abgewiesen. Job und Wait erhalten pro Eintritt eigene Aktivitätsidentitäten, bei paralleler MI mit Iterationszuordnung. Die normale Inline-Handlerausführung entfällt für diese Tasks. Die Feature-Sperre wird bereits vor automatischen Schritten im aktuellen Ausführungsmodell geprüft. Boundary-, Event-Subprozess- und verschachtelte Scope-Konstellationen werden im Preview explizit abgewiesen.

Aktuell verifiziert: frischer Release-Build, 0 Fehler/32 bestehende Warnungen. `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -class '*UnifiedParserSerializerTests' -class '*StrictSerializerRoundtripTests' -parallelMode none`: **23 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 2,805 s. Drei neue Runtimefälle prüfen deaktiviertes Feature mit Rollback, einfachen Wait sowie drei parallele MI-Waits, idempotenten Start und ausschließlich gemappte Eingaben. Nur die Policyauflösung ist ein Testdouble; Engine und SQLite-Persistenz sind real. Dies belegt weder Worker-Completion noch MI-Abschluss/Loops oder Produktionspolicy.

Katalog-Fortsetzung 2026-09-12: `ConfiguredExternalTaskContractResolver` implementiert einen lokalen, operatorverwalteten Tenant-/Topic-/Profilkatalog mit Aktivierung/Widerruf, Versionssnapshot, Limits und expliziter Transferfreigabe je Feld. Der begrenzte Dialekt `vertex.scalar-contract.v1` validiert String/Int64/Boolean-Inputs; kein allgemeines JSON Schema und noch keine verschachtelten Agentresultate. Registrierung nur bei explizitem Development/Test/UnitTest und aktiviertem Preview; Production, Stage, unbekannte oder fehlende Umgebung werden abgewiesen. Die Runtime-Tests verwenden jetzt diesen echten Resolver statt eines Policydoubles. Konfiguration und Grenzen: [Scheduling-Preview](2026-09-12_External-Agent_A02_Scheduling-Preview.md).

Letzter Nachweis: frischer Release-Build, 0 Fehler/31 bestehende Warnungen. `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskContractResolverTests' -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -parallelMode none`: **33 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 2,797 s. Einschließlich Widerruf, Fremdtenant, unbekanntem Topic/Profil, Typ-/Größen-/Versuchslimits, Transferfreigabe und Umgebungsschutz. Keine AI-/Produktionsabnahme.

Deployment-Fortsetzung 2026-09-12: Der registrierte Application-`RepositoryService` prüft External Tasks vor dem ersten Repositoryzugriff gegen `ValidateDeploymentAsync`. Preview-Aktivierung, Tenant, Topic/Profil, Limits, erforderliche/erlaubte Inputnamen und einfache Variablenreferenzen werden geprüft. Nicht unterstützte Boundary-/Subprozess-Konstellationen werden abgewiesen. Vertragsfehler einschließlich bekannter External-Task-Parserfehler werden als `BpmnDeploymentValidationException` mit `VEN-EXTERNAL-TASK-CONTRACT` weitergereicht; die bestehende Repository-API liefert dafür Bad Request. XML-Prüfung erkennt doppelte Mappingcontainer/Inputnamen sowie falsche Namespaces und ungültige Outputziele.

Letzter Nachweis: frischer Release-Build mit 0 Fehlern/32 bestehenden Warnungen. `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskDeploymentTests' -class '*ExternalTaskContractResolverTests' -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -class '*UnifiedParserSerializerTests' -class '*StrictSerializerRoundtripTests' -parallelMode none`: **44 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 2,875 s. Sieben Deployment-Servicefälle prüfen gültige Annahme und Ablehnung vor Repositoryzugriff bei deaktiviertem Feature, Fremdtenant, unbekanntem Topic, falschem Mapping, negativem Retrylimit und doppeltem Input. Kein separater HTTP-Test in diesem Lauf.

Abbruch-Fortsetzung 2026-09-12: `PersistentProcessExecutionRuntime.ExternalTasks.cs` bindet External Tasks in `CancelWaitStatesAsync` ein. Ready/Leased/RetryScheduled werden atomar Cancelled, offene Attempts geschlossen, Leasefelder entfernt und Pending-Continuations annulliert. Zugehörige aktive MI-Ausführungen werden Cancelled, damit der Prozess nicht dauerhaft wartend bleibt. Der Root-Terminate-End-Event ist im Scheduling-Preview nun zulässig. Tests lösen ihn über einen parallelen User Task nach gespeichertem External-Task-Wait aus und prüfen Job-/Tokenzustand, Historie, abgeschlossenen Prozess und keine aktive MI-Ausführung.

Letzter Nachweis: frischer Release-Build, 0 Fehler/32 bestehende Warnungen. `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskDeploymentTests' -class '*ExternalTaskContractResolverTests' -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -class '*PersistentRuntimePhase2AcceptanceTests' -parallelMode none`: **48 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 13,991 s. Nachgewiesen sind Root-Terminierung einfacher und dreifach paralleler Ready-Jobs sowie vorhandene Runtime-Regression. Lease-/Completion-Rennen, bereits laufende Attempts und Pending-Completion-Abbruch sind implementierte Pfade, aber noch nicht separat abgenommen.

Weiter offen: produktionsfähiger Katalog mit vollständiger Schemapolitik, vollständige Mapping-/Strict-/Streaming-Unterstützung, Boundary-/verschachtelte Scope-Integration, öffentliche Lösch-/Abbruch- und Suspend-Semantik, vollständiger MI-/Loop-Lebenszyklus, geschachtelte Call-Activity-Capabilityprüfung, Mehrreplikat-Konflikterholung, Constraint-/Downgrade-Negativfälle sowie E02/E09/E10 und reale PostgreSQL-Abnahme. Keine vorhandene Checkbox dieses Pakets wird durch diese Teilimplementierung als vollständig erfüllt markiert.

- [x] External-Task-Definition im Domain-Modell, Parser, Serializer und Deploymentvalidator ergänzen. Unbekannte/ungültige Profile fail-closed behandeln.
- [x] Job-/Versuchsmodell und notwendige Aktivitätszuordnung implementieren, inklusive Queue-Indizes und Constraints. Siehe A02-Prüfbericht; dies ist kein Worker-/Completion-Nachweis.
- [x] Job-Erzeugung und Wait-State in derselben Transaktion speichern; konkurrierende Duplikate anhand Unique-Key korrekt behandeln. Echter PostgreSQL-Doppelstart und Rollback lokal geprüft.
- [x] Agent-Task nicht in den bestehenden direkt abschließenden Service-Task-Pfad fallen lassen; Scope/LocalVariables sichern. Einschließlich verschachtelter und paralleler MI-Recovery.
- [x] Neue/alte DB mit PostgreSQL und zugesagtem SQLite-Verhalten testen. Lokale Migration-/Rollback-/Downgrade-Nachweise im A02-Prüfbericht; keine allgemeine Crash-Abnahme.

Abnahme: E01/E02/E09/E10. In diesem Paket darf ein deterministischer Testworker verwendet werden; das ist noch kein KI-Nachweis.

### A03 – Lease-Protokoll und Worker-Host

- [x] Claim/Heartbeat mit CAS und pro Claim neuer Lease implementieren; Batch-/Topic-/Zeitlimits begrenzen.
- [x] Alle in A03 vorhandenen API-Operationen einschließlich Status und paginierter Versuchshistorie autorisieren; Tenant, Topic, Profil, Subject, aktuelle Policy und Lease werden je Aufruf erneut geprüft. Complete/Fail folgen in A04 nach demselben Prinzip.
- [x] Separaten .NET-Worker `VertexBPMN.AgentWorker` bauen; bounded concurrency, konfigurierbares Polling mit Backoff/Jitter und Graceful Shutdown.
- [x] Heartbeat unabhängig vom langen Modellaufruf ausführen. Lease-Verlust und Shutdown brechen die Handlerausführung ab; die jeweils erneuerte Lease-Ablaufzeit wird für Transport-Retrygrenzen übernommen. Complete/Fail existieren vor A04 bewusst nicht.
- [x] API-Ausfälle, Rate-Limits, ungültige/verlorene Antworten und Tokenfehler kontrolliert als Transportzustände behandeln; sie werden nicht als fachlicher Modellfehler verbucht.

Abnahme: E03/E04/E07. Zwei tatsächliche Worker/DB-Kontexte, keine lediglich sequenzielle Nachbildung konkurrierender Claims.

### A04 – Completion, Recovery und BPMN-Semantik

- [ ] Ergebnis, Jobabschluss und persistente Fortsetzung atomar sichern. Eventuelle Outbox-Fortsetzung mit idempotentem Consumer end-to-end implementieren.
- [ ] Completion/Fail mit aktueller Lease und Aktivitätsidentität binden; veraltete oder widersprüchliche Ergebnisse abweisen.
- [ ] Retry-Backoff, Versuchslimit und absolute Deadline in allen Pfaden erzwingen, einschließlich Lease-Ablauf ohne explizites Fail.
- [ ] Fachlichen Error zum passenden BPMN-Handler propagieren; technische Erschöpfung gemäß vereinbartem Fehler-/Incident-Vertrag behandeln.
- [ ] Cancellation, interrupting/non-interrupting Boundary Events, Scope-Ende und konkurrierende Timer/Completion integrieren. Ergebnis nur in korrekten Variablenscope schreiben.
- [ ] Recovery begrenzt/paginiert und multi-replikasicher ausführen; kein vollständiger unbegrenzter Tabellenscan im Polling.

Abnahme: E05/E06/E08/E09/E10/E11 mit gezielten Prozessabbrüchen. Ein API-Erfolg darf nach Neustart nicht zu dauerhaft verlorenem Wait oder doppelter Fortsetzung führen.

### A05 – Konkreter read-only Agent

- [ ] Fachlichen Anwendungsfall und ein lokales Modell/Runtime-Profil bestätigen. Kein fest verdrahteter Anbieter und kein Cloud-Fallback für `local-sensitive`.
- [ ] `IAgentRuntime` mit genau einem echten Adapter zunächst implementieren; dessen aktuellen API-Vertrag vor Umsetzung anhand offizieller Dokumentation prüfen.
- [ ] Versionierte Promptvorlage und deterministische Tools: nur Abschnitt lesen/suchen innerhalb des Jobdokuments. Kein Modellzugriff auf Dateipfade, Shell, allgemeines HTTP oder fremde Dokumente.
- [ ] Tool-Argumente, Callanzahl, Laufzeit, Ein-/Ausgabegrößen und Tokenbudget kontrollieren. Runtime-Abbruch plus Hard-Limits; keine bloße Prompt-Anweisung als Sicherheitsgrenze.
- [ ] Schema und Belegstellen vor Ergebnisannahme prüfen. Ungültige Ausgabe nur innerhalb des expliziten Gesamtbudgets korrigieren lassen; keine unbegrenzte Reparaturschleife.
- [ ] Menschlichen User Task mit Analyse, Belegen und Unsicherheiten bereitstellen. Das Modell kann den Human-Review-Schritt nicht überspringen.
- [ ] Alten AI-Mock sichtbar getrennt halten; nicht ohne gesonderten Auftrag bestehende AI-Verträge austauschen oder Erfolg simulieren.

Abnahme: E12/E13 und Fachbenchmark. Echte Modellabnahme benötigt erreichbare Runtime; ohne sie Paket als implementiert, aber nicht real abgenommen markieren.

### A06 – Studio und Betrieb

- [ ] Agent-Preset im bestehenden Katalog, Moddle und Properties ergänzen; Profil-/Input-/Output-Auswahl und Limits verständlich darstellen.
- [ ] Roundtrip, Undo/Redo und Client-/Servervalidierung prüfen; Profile tenantgebunden laden, keine Secrets im XML.
- [ ] Jobstatus/Versuche/Deadline/Fehlercode in bestehende Betriebsansichten integrieren; Prozess/Element verlinken. Keine separate globale, ungeschützte Jobliste.
- [ ] Metriken für Queue-Alter, Lease-Verlust, Retry, Laufzeit, Schemafehler und Budgets; Inhalte redigieren. Nutzungsmessungen des Workers nicht als vertrauenswürdige Abrechnung übernehmen.
- [ ] Deployment/Start ohne verfügbares Profil eindeutig ablehnen beziehungsweise fehlenden Worker als wartenden Betriebszustand sichtbar machen.

Abnahme: E14 mit realem Studio/API; Rollen-/Tenantnegativtests, bestehende Editoraktionen bleiben erhalten. Mit dem separaten Studio-Workspace-Plan abstimmen, keine parallelen Änderungen derselben Dateien.

### A07 – Abnahme und Übergabe

- [ ] Vollständigen Beispielprozess samt synthetischen Dokumenten und Fehlerpfaden versionieren.
- [ ] E01–E14 gegen reale lokale PostgreSQL-/API-/Worker-Infrastruktur ausführen; Crash-Tests mit getrennten Prozessen, nicht nur Exceptions im selben Testprozess.
- [ ] Fachbenchmark mit festgehaltenem Modell, Prompt, Schema, Parametern und Dokumentversionen durchführen.
- [ ] Finale Regression und sauberer Checkout des freigegebenen Commits; alte Testzahlen nicht als neue Abnahme übernehmen.
- [ ] Runbook für Start, Worker-Ausfall, Lease-Recovery, Quarantäne/Incident, Modellwechsel, Aufbewahrung und Upgrade schreiben.

Abnahme: alle Pflichtfälle bestanden, keine Skips als Erfolg; bekannte Grenzen und ungetestete Engine-Modi sichtbar. Keine vollständige BPMN-/Agent-Sicherheits- oder Produktionszertifizierung aus diesem Feature ableiten.

## 6. Pflicht-Testmatrix

| ID | Szenario | Erwartung |
|---|---|---|
| E01 | BPMN importieren, speichern, exportieren, erneut parsen | Topic/Profil/Mappings unverändert; richtiger Namespace, ungültige Limits abgewiesen, null Retries erlaubt. |
| E02 | Zwei Engine-Ausführungen versuchen denselben Aktivitätsdurchlauf zu schedulen | Genau ein Job und ein konsistenter Wait; kein Unique-Fehler als scheinbarer Prozesserfolg. |
| E03 | Zwei Worker claimen gleichzeitig denselben Job | Nur einer gewinnt; SQL-Prädikate und zurückgegebene Lease stimmen überein. |
| E04 | Lease läuft ab, neuer Worker übernimmt, alter sendet Heartbeat/Complete/Fail | Alte Lease in allen Pfaden abgewiesen; neue bleibt unberührt. |
| E05 | Complete wiederholen; gleicher/abweichender Payload; fremder Aufrufer | Berechtigte identische Wiederholung idempotent, Konflikt bei Abweichung, Fremdzugriff verweigert. |
| E06 | Prozessabbruch vor/nach Job-/Wait-Commit sowie während Completion-Commit | Nach Neustart keine halben Zustände und keine doppelte wirksame Fortsetzung. |
| E07 | Fremdtenant, fehlender Claim, fremdes Topic oder selbst erfundene WorkerId | Keine Jobs/Dokumente/Statusdaten zugänglich; jede Mutation geschützt. |
| E08 | Modelltimeout, wiederholt verlorene Lease, Retrylimit=0/erreicht | Begrenzte Versuche, absolute Deadline nicht durch Heartbeats verlängert, definierter Fehlerpfad. |
| E09 | Prozessschleife sowie parallele/sequenzielle Multi-Instance-Agent-Tasks | Getrennte Jobs pro Durchlauf, richtige lokale Outputs und Join-Semantik. |
| E10 | Fachlicher Fehler, Boundary Error/Timer, Cancellation und nicht-interrupting Event | Vereinbarte BPMN-Semantik; Timer/Completion-Race hat einen konsistenten Gewinner. |
| E11 | API/Worker/ggf. Outbox-Consumer stoppen und neu starten; DB vorübergehend ausfallen lassen | Gesicherte Aufträge bleiben abarbeitbar; keine unbegrenzte Retry-/Pollinglast. |
| E12 | Prompt-Injection im Vertrag fordert Shell/Netz/anderes Dokument oder Human-Review-Bypass | Keine erweiterte Berechtigung, keine unzulässige Toolwirkung, Human Review bleibt bestehen. |
| E13 | Ungültiges/übergroßes JSON, erfundene Abschnitts-ID/Zitat, falsche Dokumentversion | Keine Übernahme als erfolgreiches fachliches Ergebnis; begrenzter Fehler-/Korrekturpfad. |
| E14 | Studio: Agent einfügen/configurieren, Undo/Redo, deployen, warten, Ergebnis menschlich prüfen | Reale persistierte Zustände und Prozessfortsetzung; kein Mock-Ergebnis als KI-Nachweis. |

Race-Tests mit Barrieren/steuerbarer Zeit oder Failpoints synchronisieren, nicht mit zufälligen Sleeps. Nach realem Crash zusätzlich DB-/Runtime-Zustand kontrollieren. Ein Engine-InMemory-Test ersetzt weder PostgreSQL-CAS noch tatsächlichen Wiederanlauf.

## 7. Fachliche Qualität und Ressourcen

Vor A05 einen kleinen versionierten Evaluationssatz vereinbaren, vorgeschlagen mindestens 20 synthetische/anonymisierte Texte: klare Klauseln, fehlende Klauseln, widersprüchliche Passagen, lange Texte, unbekannte Sprache und Angriffsanweisungen. Referenzannotation durch eine fachkundige Person; keine Selbstbewertung desselben Modells als einzige Abnahme.

Messen: erkannte relevante Kategorien, falsche Befunde, korrekte Fundstellen, übersehene kritische Stellen, Unsicherheitskennzeichnung, Laufzeit und Ressourcen pro Dokument. Fachliche Mindestwerte vor dem Lauf festlegen; nicht nachträglich passend zum Ergebnis ändern. Jede akzeptierte Fundstelle muss im referenzierten Text nachweisbar sein. Wiederholte Läufe für nichtdeterministische Ausgaben vorsehen, keine exakte Textgleichheit verlangen.

Startwerte für einen technischen Pilot, in A01/A05 zu bestätigen: 1 Job pro Worker, Lease 60 s mit Heartbeat spätestens alle 20 s, Gesamtdeadline 300 s, höchstens 2 Versuche und 8 Toolaufrufe über die gesamte Jobausführung. Token-/Dokumentlimits anhand gewählten Modells und Hardware vorab konkretisieren. Attempt-Budgets dürfen bei Wiederanlauf nicht unbemerkt zurückgesetzt werden; bei fehlenden Nutzungsdaten konservativ abbrechen statt unbegrenzt neu starten.

## 8. Offene Entscheidungen und Reihenfolge

Vor A05 erforderlich:

1. Vertragsvorprüfung als erster Fall bestätigen; Sprachen, Klauselkategorien und menschliche Prüfer benennen.
2. Lokale Runtime, konkretes Modell, Hardware und zulässige Dokumentgröße wählen. Ein Cloud-API-Modell ist kein Offline-Modell.
3. Dokumentablage, Zugriffsmodell und Aufbewahrung bestimmen; vorhandenen Store bevorzugen.
4. Fachliche Güteschwellen und Ressourcenbudgets freigeben.

Diese Entscheidungen blockieren nicht A00 oder den generischen Kernentwurf. Implementierung des neuen Features ersetzt nicht Phase 4 des [Produktionsqualitätsplans](2026-09-08_Produktionsqualitaet_Release-Abnahmeplan.md); dessen Ausfall-/Konkurrenzmechanismen möglichst zuerst qualifizieren und anschließend wiederverwenden. Bei Überschneidungen einen Integrationsverantwortlichen und getrennte Branches festlegen.

## 9. Modellunabhängiger Arbeitsauftrag und Fortschritt

Pro Paket dokumentieren: Basiscommit, Status, geänderte Dateien, Vertrags-/Migrationsänderungen, genaue Testbefehle und Namen, bestanden/fehlgeschlagen/übersprungen, lokale Artefakte, offene Entscheidungen und nächster Schritt. Kleine Commits; nur auf Nutzerauftrag committen/pushen. Originalvorschlag und fremde Änderungen nicht überschreiben.

> Lies diesen Plan und die aktuellen Repository-Anweisungen vollständig. Bearbeite ausschließlich das nächste freigegebene A-Paket. Prüfe vorhandenen Code statt Beispielklassen blind zu übernehmen. Implementiere Konkurrenz-/Tenant-/Crash-Nachweise gemeinsam mit der Funktion. Keine abgeschwächten Tests, Fake-Modellantworten als Realnachweis oder unautorisierten Toolwirkungen. Halte technische Implementierung, reale Infrastrukturabnahme und fachliche Modellabnahme getrennt. Bei fehlender Entscheidung frage konkret nach. Aktualisiere den Paketstatus und die Übergabe, ohne spätere Pakete ungefragt zu beginnen.

- [x] Implementierungsplan erstellt.
- [x] A00 – Inventur/Baseline. Ergebnis: [Inventur und Baseline](2026-09-12_External-Agent-A00_Inventur.md).
- [x] A01 – Verträge/Sicherheitsreview. [Vertrag](2026-09-12_External-Agent-A01_Vertrag.md) und [Review](2026-09-12_External-Agent-A01_Review.md).
- [x] A02 – Job/Wait/Parser/Migration. Implementierungsumfang abgeschlossen; vollständige E09/E10-Completion-Abnahme folgt nach A03/A04.
- [x] A03 – Lease-API/Worker-Grundgerüst. Nachweis: [A03-Prüfbericht](2026-09-14_External-Agent_A03_Pruefbericht.md).
- [ ] A04 – Completion/Recovery/BPMN-Semantik.
- [ ] A05 – Reale read-only Agent-Runtime.
- [ ] A06 – Studio/Betriebsintegration.
- [ ] A07 – Lokale Gesamt- und Fachabnahme.

**Nächster Schritt: A04 – Completion, Recovery und BPMN-Semantik.** A03 stellt ausschließlich den abgesicherten Claim-/Heartbeat-Pfad und Worker-Host bereit. Ohne atomare Completion/Fail-/Recovery-Semantik kann kein Job fachlich abgeschlossen werden; eine Produktionsfreigabe bleibt bis A04–A07 ausgeschlossen.

### Fortsetzung 2026-09-13 – Abbruchzustände und Schedulingidentität

- `ExternalTaskSchedulingPreviewTests`: Root-Terminate zusätzlich mit einfach/dreifach geleasten Jobs und einem bereits angenommenen Ergebnis mit Pending-Continuation geprüft. Offene Attempts werden geschlossen, Lease-/Workerfelder entfernt, die Fortsetzung wird annulliert; gespeichertes Ergebnis und Completion-Receipt bleiben erhalten. Kein Ergebnis wird als Prozessvariable übernommen. Wiederholtes Terminate über dieselbe User-Task-Idempotency-ID erzeugt keine zusätzliche Wirkung.
- Lease und angenommene Completion werden hierfür ausdrücklich als persistente Ausgangszustände angelegt. Das prüft den echten Runtime-Abbruchpfad, nicht die noch fehlende Claim-/Complete-API, echte konkurrierende Completion oder Crash-Recovery.
- `ExternalTaskSchedulingStore`: Identische Wiederholung vergleicht zusätzlich Definitionsidentität/-version, Profilversion, Versuchslimit, Deadline, Erstellzeit und MI-Zuordnung. Abweichungen führen zu `external_task_scheduling_conflict`. Regression in `ExternalTaskPersistenceTests` prüft verändertes Versuchslimit und unveränderten ursprünglichen Job.
- Frischer Release-Build: 0 Fehler, 31 Warnungen. Gezielte External-Task-Suite: **45 bestanden**. Anschließender Lauf einschließlich `PersistentRuntimePhase2AcceptanceTests`: **51 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt**, 15,494 s.

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskDeploymentTests' -class '*ExternalTaskContractResolverTests' -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -class '*PersistentRuntimePhase2AcceptanceTests' -parallelMode none
```

Historischer Zwischenstand: A02 blieb zu diesem Zeitpunkt offen. Diese lokale Auswahl ersetzte weder die Gesamtsuite noch reale PostgreSQL-Race-/Crash-Nachweise. Änderungen waren nicht committed oder gepusht.

### Fortsetzung 2026-09-13 – Migrations-Negativnachweise

`ExternalTaskPersistenceTests.MigratedSchemaRejectsInvalidJobsAndDestructiveDowngrade` ergänzt neun Fälle gegen eine isolierte SQLite-Datenbank, die über die tatsächlichen EF-Migrationen aufgebaut wird (nicht `EnsureCreated`). Direkte SQL-Änderungen umgehen bewusst den Applicationvalidator: ungültiges Versuchslimit, überschrittene Versuche, Deadline, Verfügbarkeit, negative Revision/Leasegeneration, unbekannter Status und Leased ohne Leaseidentität scheitern an den jeweils benannten CHECK-Constraints. Nach jedem Fehler wird der unveränderte Job aus einem neuen DbContext gelesen.

Der neunte Fall weist einen Downgrade mit vorhandenem Job ab und prüft den Erhalt von Job, Wait/Aktivitätsidentität, Attempt-/Continuationtabellen und Migrationseintrag. Nach expliziter Entfernung ausschließlich des synthetischen Testjobs gelingt derselbe Downgrade; der Prozess bleibt erhalten. Damit ist auch die Wiederverwendbarkeit nach dem fehlgeschlagenen Downgrade geprüft, einschließlich temporärer Guard-Tabelle und Migrationssperre.

Verifikation: frischer Release-Build mit 0 Fehlern/31 Warnungen. Obiger kombinierter Testbefehl einschließlich Runtime-Regression: **60 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt**, 15,577 s. Keine Änderung am Produktionscode erforderlich; die vorhandenen Constraints und der Downgrade-Schutz haben diese Fälle korrekt behandelt. PostgreSQL ist damit weiterhin nicht real abgenommen; Unique-/FK-Konkurrenz, vollständige Scope-/Mappingintegration und A02-Gesamtabnahme bleiben offen. Kein Commit/Push.

### Fortsetzung 2026-09-13 – Scheduling-Revisionsschutz und persistierte Wiederholung

Neue Datei `tests/VertexBPMN.Tests/Unit/Infrastructure/ExternalTaskSchedulingConcurrencyTests.cs`, acht Fälle auf tatsächlich migriertem SQLite-Schema:

- Zwei DbContexts lesen beziehungsweise ändern dieselbe Prozessrevision in festgelegter Reihenfolge. Nach dem Commit des ersten Kontexts (neuer Job oder expliziter Prozessabschluss mit Revisionserhöhung) scheitert das Scheduling des veralteten Kontexts mit `DbUpdateConcurrencyException`. Nach Rollback bleiben keine zusätzlichen Jobs, Waits oder Historieneinträge erhalten.
- Direkte EF-Inserts unter Umgehung des Stores belegen Unique-Schutz für Tenant/Aktivitätsausführung und Wait-ID sowie Fremdschlüssel für Definition und Prozess. Der fehlgeschlagene Schreibvorgang erhält jeweils das ursprüngliche Job-/Wait-Paar ohne zusätzliche Waits.
- Scheduling nach Commit aus einem frischen DbContext lädt bei identischen Daten den persistierten Job, ohne Schreibzugriff oder doppelte Historie. Veränderte Eingaben werden als `external_task_scheduling_conflict` abgewiesen; das gespeicherte Snapshot bleibt unverändert.

Die Tests verwenden getrennte Change Tracker mit gezielter Commitreihenfolge und eine gemeinsame SQLite-Verbindung. Sie sind **kein Nachweis paralleler PostgreSQL-Transaktionen**, keiner automatischen Konflikterholung und kein zusätzlicher Runtime-Abbruchnachweis: Der Prozessabschluss im Revisionsfall ist ein gezielt vorbereiteter Datenbankzustand. Mehrreplikat-Erholung/E02 und A02-Gesamtabnahme bleiben offen. Produktionscode musste für diese Fälle nicht geändert werden.

Frischer Release-Build: 0 Fehler, 31 Warnungen. Folgender Lauf: **68 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt**, 19,643 s:

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskSchedulingConcurrencyTests' -class '*ExternalTaskDeploymentTests' -class '*ExternalTaskContractResolverTests' -class '*ExternalTaskSchedulingPreviewTests' -class '*ExternalTaskPersistenceTests' -class '*ExternalTaskDefinitionTests' -class '*PersistentRuntimePhase2AcceptanceTests' -parallelMode none
```

Nächster Integrationsschritt: Konkurrenzfehler im öffentlichen Runtime-Aufrufpfad untersuchen und eine begrenzte, transaktionssichere Konflikterholung mit erneutem Laden der Prozess-/Aktivitätsidentität implementieren; kein Unique-Fehler als erfolgreicher Prozessstart. Anschließend reale PostgreSQL-Verifikation. Kein Commit/Push.

### Fortsetzung 2026-09-13 – Inbox-Konflikte gezielt behandeln

Untersuchung des öffentlichen `PersistentProcessExecutionRuntime.StartAsync`-Pfads: Der Inbox-Claim wird bereits vor der Prozessausführung gespeichert. Sein bisheriger Catch behandelte jedoch jeden `DbUpdateException` wie einen möglichen Idempotenzkonflikt. `ClaimInboxAsync` akzeptiert jetzt ausschließlich den Unique-Verstoß des konkreten Tenant/Operation/IdempotencyKey-Indexes: PostgreSQL SQLSTATE 23505 plus exakter Constraintname, SQLite ExtendedCode 2067 plus die betroffenen Inbox-Spalten. Andere Schreibfehler werden nicht umgedeutet und nicht automatisch wiederholt. Bei einem echten Inbox-Konflikt bleiben Rollback, Leeren des Trackers und Nachladen des bereits abgeschlossenen Ergebnisses erhalten.

`ExternalTaskSchedulingPreviewTests` ergänzt drei Fälle am öffentlichen Runtime-Einstieg: Ein Command-Interceptor lässt gezielt nur den ersten Inbox-Lesezugriff den vorhandenen Eintrag übersehen; das folgende INSERT verletzt den echten SQLite-Unique-Constraint. Die Runtime liefert danach die vorhandene Prozess-ID, ohne zweiten Job/Wait. Dies ist ein deterministischer Stale-Read-Failpoint, kein echter paralleler Commit. Zwei SaveChanges-Failpoints prüfen, dass ein Fremdschlüsselfehler und ein Unique-Fehler einer anderen Tabelle als ursprüngliche `DbUpdateException` weitergegeben werden, ohne Wiederholungsversuch, Prozess oder Handlerwirkung.

Verifikation: erster Build durch Sandbox-Schreibzugriff auf CLI-obj blockiert; Wiederholung außerhalb der Sandbox erfolgreich, 0 Fehler/32 Warnungen. Die 11 Previewfälle bestehen. Kombinierter Testbefehl des vorigen Abschnitts: **71 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt**, 17,616 s.

Keine allgemeine Wiederholung von `AdvanceAsync` eingeführt: Dort können bereits normale Service-Handler mit externen Seiteneffekten laufen. Sichere Wiederholung nach einem späteren Revisionskonflikt benötigt eine explizite Begrenzung auf transaktionale Ausführung oder vorherige Absicherung dieser Seiteneffekte. A02, echte PostgreSQL-Konkurrenz und automatische Erholung späterer Scheduling-Konflikte bleiben offen. Kein Commit/Push.
