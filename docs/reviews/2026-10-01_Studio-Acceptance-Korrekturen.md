# Studio-Acceptance: Korrekturen und Nachprüfung

Stand: 2026-10-01. Branch: `codex/studio-acceptance-fixes`.
Auftrag: die konkret gefundenen Studio-Fehler korrigieren, keine BPMN-/DMN-
Konformitätsassertionen abschwächen und bestehende Betriebsprofile erhalten.
Ausgangspunkt: [Abnahmebericht 2026-09-30](2026-09-30_Studio-UI-Acceptance.md).
Der ursprüngliche Bericht bleibt als Vorher-Nachweis erhalten.

## Implementierte Korrekturen

| Befund | Änderung / Quelle | Nachweis und Grenze |
| --- | --- | --- |
| F01: Fokusverlust in BPMN-Feldern | [Properties Provider](../../src/VertexBPMN.Studio/tools/bpmn-io/src/vertex-properties-provider.js): stabile Preact-Komponententypen für alle fünf Entry-Factories, keine gecachten Elementinstanzen. Properties-Panel-Bundle mit dem bestehenden Build erzeugt. | Realer Browsertest tippt den gesamten Credential-Verweis zeichenweise, prüft Fokus und Wert sowie Deploy, API-Persistenz, Reload und Export. Kein Parser-Ersatztest. |
| F02: OIDC löscht Entwurf / Kontext | [Refresh-Script](../../src/VertexBPMN.Studio/wwwroot/js/oidc-session-refresh.js): JSON-Antwort konsumieren, kein Reload bei Cookie-Erneuerung, transiente Fehler erneut versuchen. [Circuit-Provider](../../src/VertexBPMN.Studio/Services/OidcCircuitAuthenticationStateProvider.cs): alle 15 Sekunden Sitzung und aktuelle Claims revalidieren; ungültige Sitzung fail-closed. | JWT-signierte Unitfälle beweisen Rollenentzug und Tenantwechsel; echter Keycloak-/WSLC-Browsertest beweist automatischen Refresh ohne Navigation bei erhaltenem ungespeichertem Formulardraft und Tenant-Kontext. Das ist kein Nachweis aller Replikat-/Ausfallszenarien. |
| F03: erfundener Engine-Status | [ActiveEngineService](../../src/VertexBPMN.Studio/Services/ActiveEngineService.cs): circuit-scoped statt singleton, echte API-Readiness mit Timeout; Erfolg, fehlende Bereitschaft, 401/403, Netzwerkfehler und Recovery unterscheiden. [Layout](../../src/VertexBPMN.Studio/Components/Layout/MainLayout.razor) zeigt die konfigurierte API statt einer wirkungslosen Engine-Auswahl. | HTTP-Regressionen für Zustandswechsel; echte WSLC-Readiness einschließlich Datenbanken, Migrationen und RabbitMQ. Eine Mehr-Engine-Umschaltung wird nicht als implementiert ausgegeben. |
| F04: falsche Health-Werte / Abbrüche | [Monitoring](../../src/VertexBPMN.Api/Services/ProductionHealthMonitoringService.cs): fünf konkret registrierte DbContexts parallel prüfen, fehlende Registrierung und `CanConnect=false` als Fehler; UTC-Uptime; kritische Speicherschwelle zuerst; begrenzte Pings. [HTTP-DTO](../../src/VertexBPMN.Api/Health/HealthCheckResponse.cs) / [Controller](../../src/VertexBPMN.Api/Controllers/HealthController.cs): keine Exception-/Reflection-Objekte serialisieren. [Client](../../src/VertexBPMN.Studio/Services/HttpHealthService.cs): HTTP-Fehler vor Nutzdaten-Deserialisierung prüfen. | Fehlende/unerreichbare DBs und UTC-Uptime getestet. Echte MVC-HTTP-Fehlerpfade mit ausgelöster Exception liefern vollständiges 503-JSON ohne Exception-Daten. ICMP-Erreichbarkeit externer DNS-Adressen ist weiterhin keine Geschäftsabhängigkeitsprüfung. |
| F05: mehrdeutige Versionselektoren | [Lokale GUI-Tests](../../tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.cs): exakte v1-Auswahl und v2 in der Version-Spalte des Dialogs. | Versionsanzeige, tatsächlicher Viewer und persistierendes Löschen bleiben geprüft. Keine Assertion entfernt. |
| F06: WSLC-Start scheitert | [WSLC-Override](../../deploy/compose/wslc.override.yml), [Startskript](../../deploy/compose/wslc.ps1), [Adapter](../../deploy/compose/wslc-compose-compat.py), [Anleitung](../../deploy/compose/README.md): echtes Bridge-Netzwerk, Service-DNS, Loopback-Ports, Linux-RID-Publish, LF-Shellscripts, JSON/NDJSON-/Portformat-Kompatibilität und ausreichendes Startzeitlimit. | API und Studio tatsächlich als WSLC-Container gestartet, native Ersatzprozesse beendet. Acht Adaptertests. Lokale OIDC-Backchannel-Ausnahme streng begrenzt; Production und fremde Hosts/Realms abgewiesen. Docker-Basisprofil unverändert; Docker-Lauf hier nicht wiederholt. Keine automatische Restart-Policy auf WSLC behauptet. |
| F07: Tenant-Update löscht Description | [Identity-Client](../../src/VertexBPMN.Studio/Services/HttpIdentityService.cs) lädt vollständige CRUD-Daten aus `/api/tenant`. [Tenant-Seite](../../src/VertexBPMN.Studio/Components/Pages/Tenants.razor): initialisierte Bearbeitung mit Abbruch; [Kontext](../../src/VertexBPMN.Studio/Services/StudioTenantContext.cs) invalidiert Header-Liste nach Mutationen. | GUI-/API-Abgleich: Erstellen, Abbruch ohne Mutation, Name ändern bei erhaltener Beschreibung, Beschreibung ausdrücklich ändern/leeren, Header ohne Reload, Isolation und Löschen. Testhelfer sendet beim Leeren tatsächlich Backspace. |
| F08: Connector-Erfolg rot / irreführend | [Connector-Seite](../../src/VertexBPMN.Studio/Components/Pages/Connectors.razor): Erfolg und Fehler getrennt, Aktion „Check configuration“, Pflichtfelder und Mutationssperren. [HTTP-Fehler](../../src/VertexBPMN.Studio/Services/ApiResponseErrors.cs) erhalten ProblemDetails und Statuscode. | Echte GUI prüft grüne Konfigurationsmeldung und rote Disabled-Meldung ohne alte Erfolgsmeldung. Ein unerreichbarer Zielport bleibt absichtlich möglich: die Aktion ist ausdrücklich kein HTTP-Verbindungstest. |

## Tests und Reproduzierbarkeit

- Editor-Bundle: `npm run build:bpmnio` erfolgreich. `npm run test:vertex-moddle`
  erfolgreich; `npm run test:flow-validation`: 9/9.
- Release-Solution-Build mit `SkipBpmnIoAssetBuild=true` erfolgreich. Die
  Editor-Assets waren zuvor separat gebaut; bestehende Analyzer-Warnungen sind
  durch einen inkrementellen Build nicht automatisch beseitigt.
- Health-HTTP-Regression: 3/3, einschließlich vollständiger Fehlerantworten.
- WSLC-Adapter: `python deploy/compose/test_wslc_compat.py`: 8/8.
- Lokale real-backend GUI-Suite: getrennte PostgreSQL-Datenbanken pro Run-ID,
  echte API und Studio, keine CI-Ausführung. Zwei Zwischenstände 99/99 grün:
  `3cde3e960f9c4904976681bae65269cc` (511,836 s) und
  `33f40cf59d034c798985159a6dfd0924` (10592,335 s, einschließlich längerer
  Rechnerunterbrechung; keine reine Ausführungszeit).
- Erweiterte Beschreibung-Leeren-Regression deckte anschließend einen Fehler
  im Testhelfer auf (`b68adaf1c1ac48e48f721fe67b2c7071`: 98/99). Dieser wurde
  durch echte Backspace-Eingabe behoben, nicht durch Entfernung des Falls.
- Echte automatische OIDC-Erneuerung gegen WSLC/Keycloak:
  `oidc-body-drained-results.xml`: 1/1, 71,652 s. Kurze Tokenlaufzeit nur im
  isolierten Acceptance-Realm gesetzt und danach wiederhergestellt.

### Abschließender real-backend GUI-Lauf

`real-suite-b1109093b06845ca92b65cc9a8342955/results.xml`:
**99/99 bestanden, 0 Fehler, 0 übersprungen**, 444,612 s. Dieser Lauf enthält
auch die erweiterten Änderungs-/Leeren-Fälle nach der Testhelferkorrektur.
Er ersetzt für die geänderte GUI-Suite die oben genannten Zwischenstände.
Er ist weiterhin keine pauschale Freigabe der gesamten manuellen Aktionsmatrix.

### Abschließender echter OIDC-Browserlauf

`oidc-final-results.xml`: **2/2 bestanden, 0 übersprungen**, 83,274 s.
Gegen die tatsächlich laufenden WSLC-Container bestanden sowohl der vollständige
Login → API-Seiten → Cookie-/Token-Refresh → Logout als auch die automatische
Erneuerung bei erhaltenem ungespeichertem Formulardraft und Tenant-Kontext,
ohne Navigation. Die vorübergehend gesetzte Realm-Tokenlaufzeit wurde im
`finally` wiederhergestellt. Dies ist automatisierte lokale Browserabnahme,
nicht die noch ausstehende sichtbare Nutzer-Nachprüfung.

### Isolierter CI-safe-Schlusslauf

`ci-safe-isolated.log`, unveränderte Ausschlussliste aus `.github/workflows/ci.yml`:
1240 Fälle, 1231 bestanden, 8 übersprungen, **1 fehlgeschlagen** (89,018 s).
`DmnFeelDrdFullSupportAcceptanceTests.FPS_DMN_03_All_multi_hit_policies_execute_with_standard_semantics`
mit `COLLECT` / `SUM` / erwarteter Ausgabe `30` scheitert bei
`vertexFeelValidateExpression` mit `TimeoutException`. Auch ohne gleichzeitig
laufende Build-/Browser-Suite besteht damit eine Zeitlimit-Empfindlichkeit.
Das ist kein Fehler in ungültigen Testdaten und keine grüne Gesamtregression.
Keine Assertion, Fixture, Timeout-Grenze oder Workflow-Ausnahme wurde geändert.

Wiederholung nach erneutem erfolgreichem Release-Solution-Build und aktualisiertem
API-Container: `ci-safe-final-rerun.log`, **1231 bestanden, 8 übersprungen,
1 fehlgeschlagen von 1240** (87,408 s). Diesmal besteht die DMN-Klasse, aber
`Phase11HardeningTests.MemoryProfiler_LargeModel_SnapshotsMemoryUsage` verletzt
die bestehende 500-MB-Grenze (`Phase11HardeningTests.cs:166`).

Zur Eingrenzung anschließend getrennte Klassenläufe mit unveränderten Tests:

- `DmnFeelDrdFullSupportAcceptanceTests`: **8/8**, 5,947 s
  (`dmn-focused-final.log`).
- `Phase11HardeningTests`: **9/9**, 6,366 s
  (`hardening-focused-final.log`).

Die isolierten Erfolge ersetzen **keine** grüne Gesamtregression. Die Kombination
aus FEEL-Zeitlimit und Speichermessung im gemeinsam belasteten Testprozess muss
separat stabilisiert werden; die Ursache ist noch nicht abschließend bewiesen.
Nächste technische Prüfung: parallele FEEL-Engine-Initialisierung und Lebensdauer
der `ThreadLocal<Jint.Engine>` in `FeelEvaluator.cs` sowie Messumfang und
Fremdallokationen in `BpmnMemoryProfiler.cs` korrelieren. Sicherheitslimits und
Parser-Speicheranforderung beibehalten, nicht durch Skip/Timeout-Erhöhung umgehen.

Der letzte Build (`build-final.log`) ist erfolgreich. Das inkrementelle Ergebnis
mit 0 Fehlern/0 Warnungen ist kein Nachweis eines warnungsfreien Clean-Builds.
Nur der eigene API-Runtime-Container wurde für den letzten Publish ersetzt;
PostgreSQL-/RabbitMQ-/Keycloak- und State-Volumes blieben erhalten. Die API meldet
danach echte Readiness `Healthy`. Docker-Basisdatei und CI-Workflows unverändert.

Alle Rohartefakte bleiben lokal/gitignored unter
`tests/VertexBPMN.Studio.UiTests/TestResults/acceptance-2026-09-30`.
Insbesondere `compose.env` und lokale Hilfsskripte mit Testkonfiguration dürfen
nicht veröffentlicht werden. CI-Filter bleiben unverändert; Browser-, WSLC-,
Keycloak- und externe Integrationstests bleiben ausdrücklich lokale Opt-in-Läufe.

## Fehlversuche transparent erhalten

- Zu kurzer WSLC-Starttimeout lief schon während des Builds ab; Compose rollte
  Container zurück, Daten-Volumes blieben erhalten. Mit 600 Sekunden korrigiert.
- WSLC Compose betrachtete neu gebaute Images bei identischer Konfiguration als
  aktuell. Publish-Start verwendet nun Recreate/Wait; nur eigene Runtime-
  Container wurden für die Nachprüfung ersetzt, Daten-Volumes nicht gelöscht.
- Parallele Builds trafen gesperrte Windows-Testexecutables. Schlussbuild wird
  nach beendetem Runner durchgeführt; kein veralteter `--no-build`-Nachweis.
- Gleichzeitig belastete CI-/Browserläufe hatten FEEL-Zeitüberschreitungen und
  prozessweite Speicherschwellenüberschreitungen. Assertions, Konformitätsdaten
  und Workflow-Filter wurden nicht abgeschwächt. Auch die nachfolgenden
  isolierten Gesamtläufe sind noch nicht stabil; siehe Ergebnisse oben.
- Anfängliche OIDC-Runs hingen beim Chromium-Start bzw. beim Lesen einer nicht
  konsumierten Refresh-Antwort. Sie gelten nicht als bestanden.

## Grenzen der Freigabe

Diese Korrekturen beseitigen konkrete Befunde, nicht automatisch jede offene
Produktionsabnahme. Die sichtbare Nachprüfung im Codex-Browser wartet nach dem
Container-Neustart auf eine erneute Anmeldung durch den Nutzer. Die automatisierte
Keycloak-Anmeldung ist hiervon getrennt und ersetzt diese sichtbare Abnahme nicht.

Weiter offen bleiben die vollständige manuelle GUI-Aktionsmatrix, echte
OAuth2-Credential-Verwendung, grafisch erstellte HTTP-Erfolgs-/Fehlerpfade,
Call-Activity-Parent-/Child-Nachweis und konkrete Plugin-/KI-Integrationen aus
dem ursprünglichen Bericht. 99 Fälle bedeuten weiterhin nicht 99 vollständige
GUI-Funktionsnachweise: darunter befinden sich Route-Smokes und API-only-Fälle.
Keine pauschale Produktions- oder vollständige BPMN-Konformitätsfreigabe.

Die Git-Übergabe (Commit, Push und PR) ist von einer Produktionsfreigabe getrennt.
Die noch offenen Gesamtlauf- und manuellen Abnahmefälle bleiben ausdrücklich
Bestandteil des Review-Standes.
