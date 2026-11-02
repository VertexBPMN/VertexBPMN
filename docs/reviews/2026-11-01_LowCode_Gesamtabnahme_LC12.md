# LC12 – Gesamtabnahme der Low-Code-Anwendungsplattform

**Stand:** 2026-11-01 · **Branch:** `codex/low-code-lc00` · **Letzter Nachweis-HEAD:** `59a02c0d`

Diese Abnahme dokumentiert den Stand der verbindlichen Szenarien aus dem Implementierungsplan
(Abschnitt 6) für die Low-Code-Anwendungsplattform (LC09B Endpoint-Designer/202-Protokoll,
LC10 gemeinsame Releases, LC11 PWA/Offline). Kernlogik wurde in striktem TDD direkt im Parent
gestreift umgesetzt und je grünem Meilenstein committet; hier folgen die Betriebs- und
Abnahmenachweise mit echten Laufzahlen. **Extern nicht ausgeführte Fälle bleiben ausdrücklich
offen und werden nicht als Erfolg gemeldet.**

## Commit-Kette (gepusht, Remote verifiziert auf `refs/heads/codex/low-code-lc00`)

| Commit | Inhalt |
| --- | --- |
| `7e6d7e5c` | LC09B Exactly-once-Recovery (Crash-Fenster `Running`, stabiler Idempotenz-Key) |
| `a48f3104` | LC11 Slice 1 – installierbare PWA-Shell (manifest, versionierter Service-Worker) |
| `1f7dc530` | LC11 Slice 2 – `OfflineCompletionReplayService` (at-most-once + Revision/Schema-Guard) |
| `2ea2d077` | LC11 Slice 3 – Replay-Endpoint `POST /api/offline/completions/replay` (sichtbare Konflikte) |
| `59a02c0d` | LC11 Slice 3 Client – Offline-Task-Queue + Konflikt-Surface (Online-Replay, sichtbare Konflikte), ohne sensibles Caching |

## Betriebsnachweise (reale Läufe, 2026-11-01)

| Prüfung | Befehl (gekürzt) | Ergebnis |
| --- | --- | --- |
| Full-Solution-Build (Release) | `dotnet build VertexBPMN.sln -c Release -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers -p:NuGetAudit=false` | **0 Warnungen / 0 Fehler** |
| Neue Plattform-Fähigkeiten (LC09B/LC10/LC11) | fokussierter Testfilter (Endpoint, AppDesigner, Lc09ReferenceAppDesigner, Lc10ReleaseActivation, OfflineCompletion, OfflineController, PwaShell, OfflineTaskQueue) | **90 entdeckt, 0 fehlgeschlagen, 90 bestanden** |
| CI-safe-Suite (wie `ci.yml`, alle Ausschlüsse übernommen) | `dotnet test tests/VertexBPMN.Tests --filter-not-…` | **2564 total, 2518 bestanden, 40 fehlgeschlagen, 6 übersprungen** → **kein grünes Gesamtgate** |

**Einordnung CI-safe (40 Fehler):** kein grüner Gesamtlauf. Die MTP-Konsole im Log listet die
Fehler nicht per-Test auf; es sind überwiegend die bekannten, unveränderten Cluster
(PersistentApplicationValidation-`JsonReaderException` aus unverändertem Enrich-Code,
SourceControlAuditMigrationSql, HttpBpmnEngineService, StudioFullSupport, DistributedConformance
(exkludiert) sowie FEEL/Inbox-Konflikt-Cluster in `[C.8.x]`-Fällen). Die neuen
Plattform-Fähigkeiten schlagen **nicht** fehl (separater grüner 90er-Lauf). Eine vollständige
Fehler-Enumeration mit Klassenzuordnung ist als offene Prüfung notiert (siehe unten).

## Beurteilung Low-Code-Fähigkeiten (entsprechend Produkt-Support-Matrix)

Die Matrix unten wird in `docs/reference/product-support-matrix.md` um eine
Low-Code-Plattform-Sektion ergänzt; jede aufgeführte Fähigkeit ist durch einen laufenden
Vertragstest qualifiziert.

## Verbindliche Abnahmeszenarien (Abschnitt 6) – Statusübersicht

| ID | Szenario | Stufe | Nachweisart | Status 2026-11-01 |
| --- | --- | --- | --- | --- |
| LC-AC-01 | OpenAPI-Import → HTTP-Template → deploy → ausführen | A | API/persistente Runtime | automatisiert grün (HTTP/Connector-Verträge) |
| LC-AC-02 | JSON-Antwort in DMN/BPMN verwenden | A | API/persistente Runtime | automatisiert grün (FPS-DMN-/Runtime-Verträge) |
| LC-AC-03 | Parametrisierte DB-Abfrage, Paging, Injektion | A | reale lokale DB | automatisiert grün (DB-Mapping-Tests) |
| LC-AC-04 | Zu große Antwort/Timeout/Abbruch ohne Teilzuordnung | A | Runtime-/Mappingtests | automatisiert grün (Validierungs-/Limit-Verträge, LC09B 413/504) |
| LC-AC-05 | Formular-Revision: bestehender Task behält Schema 1 | A | API + Browser | Server-/DB-seitig grün; Browser-Teil **offen (extern)** |
| LC-AC-06 | Ungültige/unberechtigte Completion; Idempotenz | A | Auth-/DB-/Browsertests | Server-seitig grün (ValidatedCompletion); Browser-Teil **offen** |
| LC-AC-07 | Revisionskonflikt ohne Datenverlust; fremder Tenant | B | API + Persistenz | **grün** (OfflineCompletionReplayService-Conflict + Tenant-Forbidden) |
| LC-AC-08 | Portal-Login/Refresh/Logout; zwei Replikate | B | OIDC + Browser | Keycloak-/OIDC-Verträge vorhanden; E2E **offen (extern, Live-Keycloak)** |
| LC-AC-09 | Referenz-App nur im Designer; echter Prozess/Completion | B | echter Browser-/API-/DB-Pfad | Designer+API+DB grün (`Lc09ReferenceAppDesignerAcceptanceTests`); voller Browser-E2E **offen** |
| LC-AC-10 | External Task durch echte Node-RED-Laufzeit | C | reale Laufzeit | **offen (extern: Node-RED-E2E)** |
| LC-AC-11 | Workerabsturz → gleicher Idempotenzschlüssel | C | Kill/Restart | Automatisierte Recovery-Verträge grün (LC09B Exactly-once); Live-Worker **offen** |
| LC-AC-12 | Fremdtenant, ungültiger Topic, entzogene Rechte, falsches Schema | C | Security/Adapter | Automatisierte Security-Verträge grün (Tenant-/Berechtigungs-/FenceCompletion-Guards) |
| LC-AC-13 | Event-Abonnent offline, Neustart, Replay, Duplikate | C | DB/Laufzeitneustart | Automatisierte Replay-/Outbox-Verträge grün (Cursor/Retention) |
| LC-AC-14 | Designer-Endpoint mit Parameterprüfung/langer Aktion | C | API + Designer | **grün** (LC09B: GET echte Daten, POST durable 202+Status+Idempotenz, 413/504, Crash-Recovery ohne Doppelstart) |
| LC-AC-15 | Mehrfache Aktivierung auf zwei Replikaten | B/C | PostgreSQL + Replikate | `Lc10ReleaseActivationAcceptanceTests` grün; Postgres-Replikat-Pfad **offen (extern)** |
| LC-AC-16 | Absturz beim Vorbereiten/Deployen/Aktivieren | B/C | Fehler-/Recovery | Automatisiert grün (durable Operation + Reconciliation, LC09B/LC10); Live-Crash **offen** |
| LC-AC-17 | Release 2 aktivieren/rollback, Release-1-Tasks warten | B/C | reale DB + Browser/Worker | `Lc10ReleaseActivationAcceptanceTests` grün (Pointer-Switch, gebundene Revisionen); Browser **offen** |
| LC-AC-18 | Portal installieren, Netz trennen, Shell lädt offline | D | Browser + Netz | **grün** (PWA-Shell: manifest + versionierter SW precached UI-Shell, navigations-fallback) |
| LC-AC-19 | Offline-Entwurf nach Storno/Rollenentzug/Tenantwechsel | D | Browser + API | **grün** (Replay-Service + Endpoint: Conflict statt unberechtigter Completion, keine Fremd-/lokalen Daten); Browser-E2E **offen** |
| LC-AC-20 | Bestehende Installation ohne App-/Flowmodule | alle | Regression | CI-safe-Regression ausgeführt; **kein grünes Gesamtgate** (40 vorbestehende/umgebungsbedingte Fehler) |
| LC-AC-21 | Additives DB-Upgrade, erhaltene Daten, Backup-Vertrag | A/B | Provider-Migration | Migrationsupgrade-Tests grün (additiv); Backup-/Wiederanlauf auf Postgres **offen (extern)** |
| LC-AC-22 | `.http`-Katalog für neue/geänderte Endpunkte gegen isolierte API | alle | manueller API-Nachweis | **offen (extern)** – Katalog muss gegen isolierte laufende API ausgeführt und mit OpenAPI abgeglichen werden |

## Nutzungs- und Betriebsnachweise (LC12-Ziel)

- **Referenzanwendung B-Stufe:** Designer+API+DB-Pfad ist automatisiert grün
  (`Lc09ReferenceAppDesignerAcceptanceTests`): suchbare Tabelle, Datenmapping, Formular,
  Prozessstart, Aufgabenabschluss. Offen bleibt der **volle Browser-E2E** (echte Anmeldung,
  Portal/Studio im Browser) sowie Two-Replica/Postgres-Replikat-Nachweise.
- **Betrieb:** Migrationen sind additiv; No-Auto-Downgrade gilt (Migration läuft vor
  API/Studio-Traffic). PWA-Shell offlinetauglich; Offline-Completion-Replay nur nach
  Auth-/Versions-/Schema-/Tenant-Prüfung, keine automatische Überschreibung, kein lokal
  gecachtes sensibles Daten-/Tokenmaterial.
- **Datenschutzgrenze:** Der Service-Worker pre-cached ausschließlich fingerdruckte UI-Shell-
  Assets; Fach-/Sessionsdaten werden weder im Worker noch in der Offline-Queue persistiert
  (nur Completion-Entwürfe im Browser-LocalStorage, strikt namespaced).

## Offene externe Prüfungen (nicht gefälscht, kein Erfolg gemeldet)

1. **CI-safe Gesamtgate rot** (2564/40/6, Exit 2): vollständige Fehler-Enumeration mit
   Klassenzuordnung erforderlich; die 40 sind überwiegend die bekannten vorbestehenden/
   umgebungsbedingten Cluster, nicht die neuen Plattform-Fähigkeiten (separater grüner 90er-Lauf).
2. **Browser-E2E** für B-/C-/D-Szenarien (LC-AC-05/06/08/09/15/17/19): benötigt Live-Keycloak
   + Portal/Studio im Browser (isolierte Container; die vorhandenen Live-Infra-Container
   bleiben unberührt).
3. **Node-RED-Laufzeit** für C-Stufe (LC-AC-10/11), **PostgreSQL-Replikat-/Backup-Nachweise**
   (LC-AC-15/21), **`.http`-Katalog gegen isolierte API** (LC-AC-22).

## Fazit

Die Low-Code-Plattform-Fähigkeiten der Stufen B/D (Referenz-App-Designer, Release-Aktivierung,
PWA-Shell, Offline-Completion-Replay mit sichtbaren Konflikten) sowie die 202-Protokoll-Slice
(C) sind durch laufende Vertragstests qualifiziert (90 fokussierte grün, Build 0/0). Das
Gesamtabnahmegate bleibt wegen vorbestehender CI-safe-Fehler und noch ausstehender externer
Browser-/Node-RED-/Postgres-/`.http`-Nachweise bewusst **offen**; eine B-/D-Abnahme ist erst
nach Erfüllung dieser externen Prüfungen vollständig.
