# ADR-0008 — LC08: begrenzter Node-RED-Architekturversuch

- **Status:** Versuch ausgewertet; optionale externe Laufzeit weiterhin empfohlen. Keine Produktionsintegration/LC08-Abnahme.
- **Datum:** 2026-10-26
- **Basis:** `codex/low-code-lc00`, HEAD `07f5615a71c14605d730a1336048022b3eef144c` live geprüft.
- **Bezug:** [ADR-0003](ADR-0003-Optionale-Node-RED-Laufzeit.md), [ADR-0004](ADR-0004-Vertrauensbereiche-und-Node-RED-Sandbox.md), ADR-0005/0006/0007; Implementierungsplan LC08 und LC-AC-10..12.
- **Evidenz (lokal, wegwerfbar):** `/home/azureuser/.hermes/cache/scratch/lc08-spike/`.

## Entscheidung und Versuchsgrenze

Node-RED kann als eigener opt-in Prozess den vorhandenen ExternalTaskWorker-Vertrag nutzen. Der Versuch bleibt vollständig in Scratch: HTTP-In → ausdrücklich wegwerfbare Function → HTTP-Response, serverseitiger Helper für Standard-Tokenbeschaffung und echte Vertex-HTTP-Aufrufe. Keine Vertex-Nodes, kein Designer, keine Produktionspakete oder automatischer Start wurden implementiert. Die Teststeuerung orchestriert einzelne Schritte; der Flow ist kein autonomer produktionsreifer Worker.

Die vorhandenen Authentifizierungsbausteine wurden nicht ersetzt: echte isolierte Keycloak-Instanz, `client_credentials`, Discovery/JWKS und unveränderte produktive JwtBearer-/ExternalTaskWorker-Policy der API im `OidcTest`-Profil. Keine TestAuthenticationHandler, selbstsignierten Worker-Tokens oder Admin-Worker. Eine **separate ProcessManager-Identität** deployt/startet ausschließlich synthetische Versuchsprozesse. Ihr Worker-Claim wird tatsächlich mit 403 abgelehnt; ihre Credentials werden nicht an Node-RED weitergegeben.

## Genau geprüfte Versionen / Einrichtung

| Komponente | Pin / Prüfung |
| --- | --- |
| Node.js | **24.22.1**, native Linux x64 Runtime ausschließlich unter Scratch |
| npm dieser Runtime | **11.19.1** |
| Node-RED | **5.0.9**, direkte und transitive Auflösung in Scratch-`package-lock.json` |
| Keycloak | **26.7.3**, vorhandenes Image `quay.io/keycloak/keycloak@sha256:ff4257d0d64efbe99ed1ddfaf07765cc3c36dc7518bf8324d41961327f441c54` |
| Vertex | obiger HEAD, .NET SDK **10.0.302**, aktuelle volle Testprojekt-Dependency-Kette gebaut |
| Fach-DB | echte isolierte dateibasierte SQLite-DBs; keine InMemory-Fachpersistenz |

Offizielle Quelle, am Versuchstag abgerufen: <https://nodered.org/docs/faq/node-versions> (Stand Juni 2026), Empfehlung Node **24.x**, Node-RED 5.x mindestens Node 22. Paketmetadaten von `node-red@5.0.9` verlangen `>=22.9`. Die kompatible LTS-Linie wurde anhand <https://nodejs.org/dist/index.json> exakt gepinnt. Archiv gegen offizielle SHA-256-Liste geprüft (`versions.json`, `SHASUMS256.txt`). Der Host wurde erneut geprüft: Node 26.8.1 / npm 11.19.0 / 26G frei. **Host-Node wurde nicht verwendet.**

`install.py` ist ausdrücklich opt-in; Downloads bleiben in Scratch, npm-Lifecycle-Skripte sind deaktiviert. Kein npm-Download und kein Node-RED-Start im normalen .NET-Testlauf. System-Node, Repository-Paketdateien und Live-LC06/LC10-Dienste wurden nicht verändert.

## Inventar: tatsächlicher Vertrag

- Auth: `SecurityConfiguration.cs` erzwingt auf `/api/external-tasks` JwtBearer, authentifizierten Nutzer, Rolle `ExternalTaskWorker`, `sub`, `tenant_id`, `external_task_topic`. `MapInboundClaims=false`; normalisierte Rollen nur aus `roles` nach echtem Token-Validieren.
- `ExternalTaskWorkerPrincipal` verlangt jeweils **genau einen** nichtleeren `iss`, `sub`, `tenant_id`; exakte Topic-/Profilmengen, keine `*` oder Kommalisten. Profil nur für profilgebundene Jobs erforderlich.
- `deploy/keycloak/vertexbpmn-realm.json` enthält bereits den vertraulichen Service-Account-Client `vertexbpmn-contract-reviewer`, Audience `vertexbpmn-api`, Workerrolle, Tenant `tenant-a`, Topic `agent.contract-review`, Profil `contract-reviewer.v1`. `OAuthClientCredentialsTokenProvider` ist der bestehende Standardpfad. Der Versuch klont die Mapper in einen eigenen Realm und erzeugt kurzlebige Zufalls-Clientsecrets. **Kein notwendiger Standard-Tokenpfad-Blocker gefunden.** Die laufende fremde Keycloak-Instanz wird nicht administriert.
- Routen aus `ExternalTaskController`: POST `api/external-tasks/claim`; GET `{jobId}` und `{jobId}/attempts`; POST `{jobId}/heartbeat`, `/complete`, `/fail`.
- Claim: `topics`, `maxTasks` (1..10), `leaseSeconds` (10..120). Workeridentität nicht im Body. Tenant-/Profilabweichung filtert Claim mit **200 / leere Jobs**; unzulässiger angefragter Topic ist **403 `topic_forbidden`**.
- Heartbeat: `leaseId`, `leaseGeneration`, `leaseSeconds`; bestätigt Generation, Ablauf und Serverzeit. Complete: `leaseId`, `leaseGeneration`, `completionId`, `result`; Fail: dieselben Leasefences plus `failureId`, `kind`, `code`. Unbekannte JSON-Bodyfelder werden abgelehnt.
- Lease enthält `JobId`, `ActivityExecutionId`, `Topic`, `ContractVersion`, `AgentProfileVersion`, `Input`, `SchemaSnapshot`, `LeaseId`, `LeaseGeneration`, `LeaseExpiresAt`, `Deadline`, `AttemptNumber`, `MaxAttempts`. Zeiten UTC Unix-Millisekunden. Diese Antwortdaten wurden unverändert für Mutationen übernommen.
- Persistenz: Job speichert Definition-ID/-Version/-Snapshot, Vertrags-/Profilversion, Tenant, Prozess, WaitToken, ActivityExecution, Leasegeneration, Workerissuer/-subject und Completion-ID/-Hash. CAS-Guards und frische Vertragsprüfung verhindern alte Lease-/Policy-Fortsetzung. Recovery-Pump pollt alle fünf Sekunden.
- **Release-Lücke:** `ProcessInstance.ApplicationReleaseId` besteht, aber `ExternalTaskLease` und `ExternalTaskJob` liefern keinen expliziten ApplicationRelease-/Flowrevision-Identifier. Im Versuch ist `applicationReleaseId=null`. Definition-/Vertragssnapshot ist keine geprüfte Flow-Releasebindung. Nicht ohne TDD erweitern.
- Resolver: `ConfiguredExternalTaskContractResolver` akzeptiert begrenzte skalare String/Boolean/Integer-Felder, ein Objekt als Feldcontainer; keine Objekt-/Array-Inputs. Synthetischer Flow braucht keine Erweiterung und keinen JSON-in-String-Workaround. Ergebnis `approved` muss Boolean sein.
- Bestehende Tests: `ExternalTaskApiContractTests`, `ExternalTaskLeaseServiceTests`, `ExternalTaskContractResolverTests`, `ExternalTaskCompletionAcceptanceTests`; außerdem Scheduling-/Boundary-/Deployment-/Postgres-Acceptance, Operations-, Worker- und Handler-Tests vorhanden. Letztere sind Inventar, nicht alle ausgeführt.

## Ausführung und Ergebnisse

Alle drei **begrenzten Machbarkeitsfragen** sind VALIDATED; die vollständigen LC-AC-10..12 bleiben offen.

| Frage | Urteil | Tatsächliche API-/DB-Wirkung |
| --- | --- | --- |
| 1: eigener Node-RED-Prozess → echte Worker-Auth → Claim/Heartbeat/Completion | **VALIDATED** | Standard-Tokenbeschaffung 200; Node-RED Claim 200 mit echter Lease; gültige Heartbeats 200 mit bestätigter Generation/Ablauf; Completion 200 `Completed` / `Pending`; Recovery schreibt `Applied`, genau eine Benutzeraufgabe `after` pro Instanz, `review.approved=true` in Fach-DB. Anonymer Claim 401; ProcessManager ist kein Worker (403). |
| 2: Kill nach externer Wirkung vor Completion, Restart/Reclaim/Idempotenz | **VALIDATED** | Unabhängiger Python-HTTP-Testdienst persistiert Wirkung in eigener `effects.db` mit UNIQUE-Key = stabiler `ActivityExecutionId`. Node-RED während gehaltenem Verarbeitungsschritt SIGKILL, Exit **-9**. Nach Ablauf/Recovery/Restart Claim desselben Jobs mit neuer Lease und Generation **1 → 2**, Attempt **1 → 2**. Alte Completion **409 `lease_lost`**, DB-Snapshots vor/nach exakt gleich. Wiederholter echter HTTP-Aufruf desselben Fachkeys: **zwei Aufrufe, eine Wirkungszeile**. Neue Completion 200; genau eine Fortsetzung und Aufgabe für diesen Job. |
| 3: falscher Tenant/Topic/Profil und falsches Ergebnis | **VALIDATED** | Gegen tatsächlich Ready-Job: Tenant/Profil Claim 200 mit `jobs=[]`, Topic Claim 403. Gesamte beobachtete Runtime-Tabellen vor/nach diesen Claims gleich. Fremde Tenant-/Topic-/Profil-Completion jeweils **404 `external_task_not_found`**. Falscher Boolean-Typ **422 `result_schema_invalid`**, exakt identische DB-Snapshots vor/nach. Erst gültiges Ergebnis schafft Fachfortsetzung. |

Endzustand des abschließenden sauberen Laufs: **2 Jobs Completed, 3 Attempts, 2 Continuations Applied, 2 Benutzeraufgaben, 2 Instanzen mit geprüftem Output, 0 RuntimeInbox-Zeilen, 4 RuntimeOutbox-Zeilen** (je ProcessStarted/UserTaskCreated). Keine Completion-Inbox-Zeilen erfunden; External Tasks verwenden ihre eigene Receipt-/Continuation-Persistenz. `PRAGMA integrity_check=ok`. `verify.py` prüft diese Assertions tatsächlich.

Relevante Evidenz: `results.json`, `summary.json`, `run.log`, `api.log`, `node-red.log`, `idp.log`; `db-leased.json`, `db-before-invalid.json`, `db-after-invalid.json`, `db-completed.json`, `db-before-stale.json`, `db-after-stale.json`, `db-recovered.json`, `BpmnDbContext.db`, `effects.db`. Nur synthetische Daten; keine Bearertokens, Clientsecrets oder Live-Verbindungsdaten werden als Evidenz gespeichert. Ephemerer Realm mit Secrets und Node-RED-intern generierter Credential-Key werden nach Auswertung entfernt.

## Start-/Stop- und Rechteentscheidung

- **Native geprüft:** absolute gepinnte Node-Runtime, eigener Userdir/Flow/Settings, Loopback 58810. Python-Runner besitzt die Kinder; SIGTERM mit Timeout/SIGKILL-Fallback und `wait` im `finally`. Eigene API 58809, eigener IdP-Container 58808, eigener Fachtestdienst 58811. Nach Lauf alle Kinder beendet, nur eigener IdP-Container entfernt.
- **Existing-Service:** Architektur weiterhin zulässig, hier nicht qualifiziert. Operator muss Basisadresse, Clientidentität, Claims und Versionen explizit bereitstellen; keine übernommene Live-DB/Testidentität.
- **Optional Container/WSLC:** IdP-Container geprüft; Node-RED-Container/WSLC selbst nicht geprüft. Keine Containerpflicht für Vertex.
- **Editor/Admin im Versuch:** `httpAdminRoot=false`, `disableEditor=true`, Loopback. Daher keine ungesicherte Editor-/Deployoberfläche; der Wegwerf-HTTP-Testeingang ist nur lokal und keine Produktionssicherheitsgrenze.
- **Offene Produktionsanforderungen:** separate Adminauth; getrennte Edit-/Deployrechte; deploy-authorisierte immutable Flowrevisionen; Runtime je Tenant/Vertrauensgruppe; begrenzte Netze/Dateizugriffe, CPU/RAM/Timeouts; eng begrenzte Credentialauflösung und Rotation; kuratierte versionierte Paket-Allowlist/SBOM/Signatur-/Updateprüfung. Im Versuch nur Node-RED-Kernnodes, Paletteinstallation/Uploads und automatische Moduleinstallation deaktiviert. Dies ist **keine Sandbox-Abnahme**.

## Einschränkungen, Fehler und kleine Folgeslices

- Kein Produktionscode geändert; keine TDD-Korrektur notwendig. Kein vollständiger Node-Adapter, Workflowdesigner, Secret-Node, Admin-/Deployment-/Package-Isolationstest, Rollen-/Profilwiderruf, Antwortverlusttest oder Releasewechselnachweis. Keine vollständige LC-AC-10..12-Abnahme.
- SQLite beweist echte relationale Persistenz in einem API-Prozess, keine PostgreSQL-Konkurrenz-/Replica-Abnahme. Node-RED-Context bleibt flüchtig, Engine-/Fachpersistenz wird nicht durch Context ersetzt.
- Setupfehler wurden diagnostiziert und ausschließlich im Scratch-Runner korrigiert: Keycloak verlangt Realm-Dateinamen passend zum Realm; dessen vorhandene Clientrollen benötigen `vertexbpmn-api` beim Import; deaktivierte `ExternalTasks:Enabled` führte zu Controller-DI-500, opt-in aktiviert. Diese früheren Fehlversuche sind **nicht** als grüne Abnahme gewertet.
- Erster Build erreichte Tooltimeout vor Testprojektabschluss; erneuter koordinierter Build der **vollen Dependency-Kette** erfolgreich, **0 Warnungen/0 Fehler**. `NuGetAudit=false` nur per Aufruf, keine Projektänderung.
- Direkter Test-AppHost mit vier exakten Klassenfiltern: **Total 52 / Errors 0 / Failed 0 / Skipped 0 / Not Run 0** (`regression.log`). Unit-/SQLite-Regression ist nicht echte OIDC- oder Node-RED-Abnahme; diese stammt ausschließlich aus dem gesonderten Versuch.

Nächste kleine, getrennt zu beauftragende RED→GREEN-Slices:

1. Minimaler Vertex-Konfigurations-/External-Task-Eingangsnode; Standard-Tokenprovider, exakte Claims und vollständig erhaltene Leasedaten, ohne Produktionsautostart.
2. Heartbeat-/Cancellation-Guard plus Completion-/Fail-Node: stale Lease, Retry-After, Antwortverlust und stabile Completion-/Fachkeys zuerst als echte failing behavior tests; anschließend reale Recovery-Wiederholung.
3. Explizite ApplicationRelease-/Flowrevision-Metadaten mit abwärtskompatiblem API-Vertrag; alten Job nach Releasewechsel prüfen, statt aktuelle Revision still einzusetzen.
4. Nur falls Referenzflow benötigt: begrenzter versionierter Objekt-/Array-Vertragsresolver mit Legacy-Regression.
5. Separater Runtime-/Admin-/Package-Security-Slice mit echtem Rechtewiderruf, Secretzugriffsverweigerung und opt-in nativen/Existing-Service-Profilen; dann gezielte vollständige LC-AC-10..12, einschließlich PostgreSQL und unabhängiger Fachwirkung.

Alles uncommitted; kein Commit/Push. Fremde `.agents/skills` und `skills-lock.json` bleiben unverändert.
