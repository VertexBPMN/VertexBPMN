# LC10 – Gemeinsame Aktivierung, Rollback und Provenienz von Applikations-Releases

Dieses Runbook beschreibt die persistente Release-Aktivierung über die **Application-Release-API**
(LC10). Eine freigegebene, revisionsgesicherte Applikation wird als **Release-Generation**
vorbereitet, aktiviert und kann ohne Datenverlust zurückgerollt werden. Die Kernidee: Der
externe API-/Release-Deploy ist **kein** verteiltes SQL-Transaktionsproblem, sondern wird über
eine **durable Operation** mit eindeutiger **Operation-Identity** und einen **Rekonziliations-Pass**
modelliert. Damit werden Antwortverluste, Crashs und parallele API-Replikate deterministisch
abgefangen.

Alle Endpunkte sind tenantgeprüft (Application-Id + Tenant-Claim). Mutierende Endpunkte erfordern
`ProcessManager` (oder `Admin`), lesende `ReadOnly`.

## Operationspfad

```
Validate → Prepare → Activate → Status → Rollback
                     \-> Cleanup (separat benannt)
```

- `POST /api/applications/{id}/releases/validate` – gehärteter Import eines Release-Archivs/Manifests prüfen, **ohne** etwas zu persistieren.
- `POST /api/applications/{id}/releases/prepare` – Manifest + Referenzgraph validieren und als `Prepared` speichern; aktiviert **nie** automatisch.
- `POST /api/applications/{id}/releases/activate` – Deploy + stufenloses Aktivieren mit **atomarem, revisionsgeprüftem Pointer-Switch**.
- `GET /api/applications/{id}/releases/status` – aktueller Dauerzustand, **rekonziliert** jede in-flight DeploymentRunning-Operation.
- `POST /api/applications/{id}/releases/rollback` – kehrt den Aktivierungszeiger auf eine frühere Generation um (neue Starts).
- `POST /api/applications/{id}/releases/cleanup` – **separat benannter** Request: entfernt nur nie aktivierte Pre-Release-/Fehlversuchsdaten, fasst den aktiven Zeiger nicht an.
- `GET /api/applications/{id}/releases/active-start` – konkrete, tenantgeprüfte `ProcessDefinitionId` der aktiven Generation.

## Idempotenz & Operation-Identity

Jeder Prepare-/Activate-/Rollback-Aufruf trägt eine **Operation-Identity** (`operationId`). Der
eindeutige DB-Index `{TenantId, ApplicationDefinitionId, OperationId}` erzwingt, dass Wiederholungen
mit **derselben** Operation-Identity auf **dieselbe** Release-Generation treffen und idempotent
denselben Zustand zurückgeben – es entsteht nie eine zweite aktive Generation. Retries im
`application-releases.http`-Kollektionsbeispiel verwenden bewusst dieselbe Operation-Identity.

## Zustandsmaschine

```
Prepared ──(Activate)──► DeploymentRunning ──(Rekonziliation)──► Ready ──(Pointer)──► Active
   │                                                                                      ▲
   └──(Referenzgraph nicht satisfiierbar)──► Failed ──(Rollback/Rekonzil.)──► Recovered ──┘
```

- `Prepared` – Manifest + Referenzgraph validiert, noch nicht deployt.
- `DeploymentRunning` – Deploy ist in-flight; ein Crash hier ist rekonzilierbar (durable Operation).
- `Ready` – Deploy als abgeschlossen beobachtet, Zeiger noch nicht gesetzt.
- `Active` – der **einzige** gesetzte aktive Zeiger; neue Starts binden diese Generation.
- `Failed` – Deploy/Zeigerwechsel fehlgeschlagen und sichtbar.
- `Recovered` – ein zuvor fehlgeschlagener/unvollständiger Versuch wurde zurückrekonziliert.

## Provenienz & atomarer Pointer

- **Manifest & Provenienz**: `ManifestJson` (schemaVersion, artifactKind, key, revision, hash,
  dependencies, target profile, credential alias) sowie die Git-Provenienz (`GitCommitSha`,
  `GitRepository`) aus dem G10-Modellstack werden unverändert geführt – niemals durch den Aktivierungs­pfad mutiert.
- **Atomarer, revisionsgeprüfter Pointer-Switch**: `PointerRevision` ist ein optimistischer
  Concurrency-Token. Konkurrierende API-Replikate lesen dieselbe Zeile und kämpfen per **CAS** auf
  `PointerRevision`; der Verlierer lädt neu, rekonziliert und konvergiert auf **genau einer**
  aktiven Generation. Ein wiederholter Aktivierungsaufruf ergibt stets genau einen konsistenten
  aktiven Zustand.

## Replikat-/Parallelverhalten

Zwei API-Host-Instanzen gegen **denselben Store** (gleiche Migration) konvergieren deterministisch:
parallele Aktivierungsaufrufe mit derselben Operation-Identity ergeben **exakt einen** `Active`-Eintrag
(CAS + Rekonziliation). Ein zwischen Preparieren und Zeiger-Switch abgebrochener Lauf wird beim
nächsten `status`/`activate` deterministisch rekonziliert (`Ready`→`Active` bei satisfiierbarer
Revision, sonst `Failed`).

## Rollback-Semantik

Rollback kehrt `PointerRevision` der aktiven Generation auf `Recovered` und die einer früheren,
noch tragbaren Generation auf `Active` zurück:

- **Neue Starts** (Auflösung über `active-start`) wechseln sofort auf die zurückgesetzte Generation.
- **Bereits gebundene, laufende Operationen** der alten Generation laufen **weiter** – ihr
  konkreter `ProcessDefinitionId` bleibt gültig und wird nicht invalidiert.

## Wichtige Hinweise

- Cleanup ist bewusst von Rollback getrennt, damit isolierte Testdaten bereinigt werden können,
  ohne den Aktivierungszeiger zu berühren.
- Deploy-Aufrufe nach einem `Failed`/`Recovered`-Zustand sind blockiert (`ApplicationReleaseCompatibilityException`).
- Bei SQLite ist der Parallelbetrieb über atomare CAS + Rekonziliation gelöst (keine verteilten
  Sperren). Für eine echte Datenbank denselben Store für beide Replikate verwenden.
