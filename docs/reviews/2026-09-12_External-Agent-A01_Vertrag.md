# A01 – External-Task-Vertrag v1

Stand: 2026-09-12. Basis: `9f65335a45e61b50d421b05850e0e8115dc69d65`.
Status: implementierbarer Kernvertrag; Laufzeitimplementierung folgt in A02–A04.
Geltung: [Umsetzungsplan](2026-09-09_External-Agent-Vertragspruefung_Umsetzungsplan.md), [Inventur](2026-09-12_External-Agent-A00_Inventur.md).

## 1. Architektur und Geltungsbereich

Der erste unterstützte Modus ist `PersistentProcessExecutionRuntime`. Simple und Legacy-Distributed müssen Modelle mit External Tasks vor Ausführung explizit mit `external_task_engine_unsupported` ablehnen. Das gilt auch für geschachtelte und aufgerufene Prozesse. CMMN ist nicht Teil von v1.

Domain enthält Definition, Zustand und persistente Entitäten. Application enthält Orchestrierung und Schnittstellen, Infrastructure SQL/CAS, Migrationen und Recovery, API Autorisierung und DTO-Übersetzung. Ein eigener Worker führt externe Arbeit aus. Keine Domain-Abhängigkeit auf Application.

Ergebnisannahme und persistente Fortsetzung werden in derselben relationalen Datenbank gesichert. Ein interner Continuation-Processor nimmt diese Fortsetzungen auf. Die allgemeine Broker-Outbox bleibt für Betriebsereignisse verfügbar; ein Broker ist für External-Task-Fortsetzung nicht erforderlich.

## 2. BPMN-Vertrag

```xml
<bpmn:serviceTask xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
                  id="ReviewContract" name="Vertrag vorprüfen">
  <bpmn:extensionElements>
    <vertex:externalTask topic="agent.contract-review"
                         agentProfileRef="contract-reviewer.v1"
                         maxRetries="1" deadlineSeconds="300" />
    <vertex:ioMapping>
      <vertex:input name="documentId" expression="documentId" />
      <vertex:input name="documentVersion" expression="documentVersion" />
      <vertex:output name="result" target="contractReview" />
    </vertex:ioMapping>
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

Dies ist ein neues Vertragsbeispiel, kein bereits ausführbares Modell. Genau ein `externalTask` ist auf einem Service Task erlaubt. Andere Namespace-URIs werden für diesen neuen Typ nicht als Alias interpretiert. Gleichzeitige Connector-, Script-, AI- oder fremde Worker-Implementation ist ein Deploymentfehler; leeres BPMN-`implementation` beziehungsweise `##unspecified` ist zulässig.

`topic`: 1–128 ASCII-Zeichen, Muster `[a-z][a-z0-9.-]*`; keine Wildcards. `agentProfileRef` ist für `agent.*` verpflichtend, sonst verboten. Generische Topics werden über einen tenantgebundenen, operatorverwalteten Topic-Vertrag mit unveränderlicher Version, Input-/Output-Schema und Fehler-Allowlist freigegeben. Agentprofile erweitern diesen Vertrag. Unbekannter Topic-Vertrag oder unbekanntes, gesperrtes oder fremdes Profil verhindert Deployment und erneutes Scheduling.

`maxRetries`: erforderliche Ganzzahl 0–9; `maxAttempts = checked(maxRetries + 1)`. `deadlineSeconds`: erforderliche Ganzzahl 1–86400, zusätzlich durch den Topic-/Profilvertrag begrenzt. Operatorlimits können nur einschränken. Verletzungen werden abgewiesen, nicht still korrigiert. Deadline beginnt beim ersten dauerhaften Scheduling und umfasst Queue-Wartezeit, alle Versuche und Backoff.

Inputs nutzen die vorhandene Ausdrucksauswertung im lokalen Aktivitätsscope, vor Scheduling. Nur deklarierte Inputs werden kopiert. Fehlende Pflichtwerte, Schemafehler und Credential-/Secret-Referenzen verhindern Scheduling. Ein Inputfehler erzeugt einen redigierten Incident, keinen wartenden Job. A02 prüft den tatsächlichen Ausdrucksdialekt mit dem obigen Variablenreferenz-Beispiel.

Outputs: v1 bietet genau den Envelope-Namen `result`; höchstens ein Output-Mapping schreibt das validierte Ergebnisobjekt in eine explizit deklarierte lokale Variable. Zielnamen dürfen keine Systemvariablen, Credentialwerte oder Scope-Pfade überschreiben. Kein Mapping bedeutet Ergebnis speichern, keine Variable schreiben. Kein impliziter Merge beliebiger Worker-Felder. Parallele Multi-Instance schreibt ausschließlich in den jeweiligen lokalen Scope; Aggregation erfolgt über die vorhandene MI-Semantik.

Definition, Topicvertrag, Input-/Output-Schema, Mapping und optionales Agentprofil werden als unveränderlicher sicherer Snapshot mit Version und SHA-256 gespeichert. Policy-Widerruf bleibt zusätzlich live wirksam. XML enthält keine Provider-URL, Secrets oder freien Toolrechte.

## 3. Aktivitätsidentität und Persistenz

Neue Entitäten: `ExternalTaskJob`, `ExternalTaskAttempt`, `ExternalTaskContinuation`. `ActivityExecutionId` wird beim Eintritt in diesen konkreten Aktivitätsdurchlauf erzeugt und auf dem persistenten Wait-Token gesichert. Scheduling-Wiederholungen lesen dieselbe ID. Loop-Wiedereintritt und jede MI-Instanz erhalten eine neue ID. Die Element-ID oder eine wiederverwendete Token-ID allein reicht nicht.

Jobfelder: Id, TenantId (nicht nullable), ProcessInstanceId, DefinitionId/Version, ActivityId, ActivityExecutionId, WaitTokenId, ScopeExecutionId, optionale MultiInstanceExecutionId/Index, Topic/Vertragsversion, optionale Profilversion, InputSnapshot, DefinitionSnapshot, SchemaSnapshot, State, Revision, CreatedAt, AvailableAt, Deadline, AttemptsStarted, MaxAttempts, LeaseId, LeaseGeneration, LeaseExpiresAt, WorkerIssuer/Subject, Result, CompletionId, ResultHash, CompletedAt, redigierter ErrorCode.

Constraints: Unique `(TenantId, ActivityExecutionId)`; konsistente Tenant-/Prozess-/Tokenzuordnung mittels zusammengesetzter Referenzen beziehungsweise äquivalenter transaktionaler Validierung; `0 <= AttemptsStarted <= MaxAttempts`, `1 <= MaxAttempts <= 10`, Generation/Revision nicht negativ. Zustandsabhängige Lease-Felder müssen konsistent sein. Queue-Indizes `(TenantId, Topic, State, AvailableAt, Id)` und `(State, LeaseExpiresAt, Id)` sowie `(State, Deadline, Id)`.

Attempt: Unique `(JobId, AttemptNumber)` sowie `(JobId, LeaseGeneration)`; Lease-ID, Issuer/Subject, Beginn/Ende, Endegrund, redigierter Fehlercode. Keine Prompt- oder Dokumentkopien. Continuation: Unique `(JobId)`; Outcome (Success/BusinessError/TechnicalFailure/Timeout), ActivityExecutionId, State Pending/Applied/Cancelled, Revision und Zeitstempel. Sie referenziert den Job, dupliziert den Ergebnisinhalt nicht.

Providerübergreifend werden Zeitpunkte als UTC-Unix-Millisekunden (`long`/`bigint`/SQLite INTEGER) persistiert; API nutzt ISO-8601 UTC. Abfragen vergleichen numerische Spalten direkt. `TimeProvider` liefert ausschließlich Serverzeit. NTP ist Betriebsvoraussetzung; Clock-Skew zwischen Replikas wird überwacht. Keine Workerzeit beeinflusst Lease oder Deadline. JSON wird in v1 als UTF-8-kompatibler Text gespeichert, ohne providerabhängige JSON-Abfragen.

Migrationen additiv: drei neue Tabellen, nullable Aktivitätsdurchlauf-/Scope-Zuordnung am Token soweit erforderlich, Constraints und Indizes. Bestehende Tokens/Timer bleiben unverändert. Kein Backfill fingierter External Tasks. Neuinstallation und Upgrade mit befüllter PostgreSQL- und SQLite-Datenbank prüfen. Downgrade vor Entfernung der Tabellen verweigern, solange Jobs oder unapplizierte Fortsetzungen existieren; Operator muss drainen und sichern. Kein automatisches Löschen aktiver Arbeit.

## 4. Zustände und atomare Operationen

| Ausgang | Auslöser/Prädikat | Ziel | Atomare Wirkung |
|---|---|---|---|
| kein Job | autorisierter, gültiger Eintritt | Ready | Job + Wait + Boundary-Registrierungen + Historie; keine ausgehenden Tokens |
| Ready | AvailableAt <= now < Deadline, AttemptsStarted < MaxAttempts, Policy aktiv | Leased | neue Lease-ID, Generation und AttemptsStarted jeweils +1, Attempt anlegen |
| Leased | Heartbeat, aktuelle nicht abgelaufene Lease | Leased | LeaseExpiresAt = min(now + bewilligte Dauer, Deadline) |
| Leased | gültige Completion vor Deadline | Completed | Ergebnis + Receipt + Success-Continuation speichern, Lease schließen |
| Leased | retrybarer Fehler oder LeaseExpiresAt <= now, Budget verfügbar | RetryScheduled | Attempt schließen, Lease ungültig machen, AvailableAt setzen |
| RetryScheduled | AvailableAt <= now < Deadline | Ready | Claim wieder ermöglichen; verbrauchte Versuche bleiben erhalten |
| Leased | fachlicher erlaubter Fehler / erschöpfte Versuche | Failed | Attempt schließen + passende Fehler-Continuation |
| Ready/Leased/RetryScheduled | now >= Deadline | TimedOut | Lease schließen + Timeout-Continuation |
| Ready/Leased/RetryScheduled | Prozess-/Scopeabbruch oder interrupting Boundary gewinnt | Cancelled | Lease schließen, Wait deaktivieren; Abbruchpfad atomar sichern |
| Completed/Failed/TimedOut | Continuation noch Pending, Abbruch gewinnt | gleicher Jobzustand | Continuation Cancelled, keine Outputs/Erfolgsfortsetzung |
| terminal | neue Mutation | unverändert | Konflikt; nur berechtigte identische Completion-Wiederholung ist quittierbar |

Terminale Jobzustände beschreiben die externe Arbeit. `Completed` bedeutet dauerhaft angenommen, noch nicht zwingend im Prozess angewendet. Betriebsansicht zeigt zusätzlich `continuationState`. Ein späterer Prozessabbruch nimmt die bereits erfolgte Annahme nicht zurück, verhindert aber ausstehende Outputs/Fortsetzung.

Alle konkurrierenden Operationen sperren/CAS-validieren in gleicher Reihenfolge Prozessinstanz → Aktivitäts-Wait → Job → Attempt/Continuation. PostgreSQL nutzt kurze Zeilensperren plus Revisionen; SQLite einen kurzen serialisierten Schreibvorgang plus Revisionen. Prozessrevision muss auch bei Cancel/Boundary und Continuation beteiligt sein. Ein isoliertes Job-CAS reicht nicht für das Rennen gegen Prozessabbruch.

Nach Erwerb der relevanten Sperren Serverzeit erneut lesen und Prädikate prüfen. Timeout gewinnt bei `now == Deadline`; alte Lease gilt bei `now == LeaseExpiresAt` nicht mehr. Ein vor Deadline linearisiertes Complete darf danach bestätigt werden. Zuerst wirksam gesicherter Abbruch verhindert Completion. Nach Ergebnisannahme aber vor Fortsetzung kann ein Abbruch weiterhin die Fortsetzung annullieren.

Complete prüft Schema und Payloadlimits vor kurzer Transaktion; innerhalb nochmals unveränderte Schema-/Policyversion, Identität, Lease, Deadline und Wait prüfen. Speichern: Result + Receipt + terminaler Job + Pending-Continuation, gemeinsam committen. Keine Netzwerkarbeit in der Transaktion.

Continuation-Processor: begrenzte Auswahl, separate Transaktion je Prozess; Prozess/Wait/Job sperren, Pending prüfen, Output in richtigen Scope übernehmen, Boundary-Waits auflösen, ausgehende persistente Tokens beziehungsweise Fehlerpfad erzeugen und Continuation Applied gemeinsam committen. Danach normale Runtime separat anstoßen. Nachfolgende synchrone Service-Task-Handler dürfen nicht innerhalb dieser Annahme-/Fortsetzungstransaktion aufgerufen werden. A04 muss dafür bei Bedarf `AdvanceAsync` in persistente Planung und spätere Ausführung aufteilen.

Crash vor Commit hinterlässt keine halbe Wirkung. Crash nach Commit hinterlässt Pending-Tokens oder Pending-Continuation, die ein tatsächlich implementierter Recovery-Consumer wieder aufnimmt. Nach Wiederholung von Applied werden weder Outputs noch Tokens erneut erzeugt.

## 5. Worker-Authentifizierung

Worker-API erzwingt das vorhandene validierte Bearer-Scheme explizit, auch wenn lokal API-Key Default ist. Eigene Rolle `ExternalTaskWorker` wird in `VertexOidcClaims` als erlaubte Rolle ergänzt, gewährt aber keine bestehenden Operator-/Studio-Policies. Bearer-Principals aus allen unterstützten Issuer-Konfigurationen müssen einheitlich validiert werden.

Erforderlich: genau ein nichtleeres `iss`, `sub`, `tenant_id`, Rolle `ExternalTaskWorker` und mindestens ein `external_task_topic`. Für Agentjobs zusätzlich `external_task_profile`. Mehrere Topic-/Profilclaims sind exakte Listen, ohne Wildcards. Workeridentität ist `(iss, sub)`; Client-Credentials-Token verwenden. OIDC-Provider bleibt austauschbar; Keycloak-Mapper ist ein Deploymentbeispiel für diese neutralen Claimnamen. `Admin` allein reicht nicht. Generische Development-API-Keys erhalten keine impliziten Workerrechte.

Jede Route prüft Policy und jede Joboperation anschließend Tenant, Topic, Profil, Subject und soweit erforderlich Lease. Claimfilter verleihen keine Folgerechte. Ein fremder oder unsichtbarer Job liefert 404; fehlende Workerrolle/Topicberechtigung auf Claim liefert 403. Widerruf des Topic-/Profils wird aus lokalem serverseitigem Policyzustand bei jeder Operation geprüft. JWT-Rollenwiderruf wird spätestens bei Tokenablauf wirksam; Sofortsperre eines Workers erfolgt über serverseitige Sperrliste für `(iss, sub)`, nicht durch eine behauptete sofortige JWT-Revocation.

## 6. API v1

Basis `/api/external-tasks`. JSON camelCase; unbekannte DTO-Felder werden abgewiesen. Request-Bodies enthalten kein `tenantId`/`workerId`. Serverseitige Limits: Request 256 KiB, Input pro Job 128 KiB, Result 128 KiB, JSON-Tiefe 32. Operatorprofile dürfen kleinere Limits setzen. UTF-8-Bytes zählen, nicht Zeichen. Keine Logausgabe des Bodys.

| Route | Request | Erfolg |
|---|---|---|
| POST `/claim` | `{ "topics":["agent.contract-review"], "maxTasks":1, "leaseSeconds":60 }` | 200 `{ "jobs": [...] }`, leere Auswahl erlaubt |
| POST `/{id}/heartbeat` | `{ "leaseId":"UUID", "leaseGeneration":1, "leaseSeconds":60 }` | 200 mit leaseExpiresAt/serverTime |
| POST `/{id}/complete` | `{ "leaseId":"UUID", "leaseGeneration":1, "completionId":"UUID", "result":{} }` | 200 `{ "jobId":"UUID", "state":"Completed", "continuationState":"Pending" }` |
| POST `/{id}/fail` | `{ "leaseId":"UUID", "leaseGeneration":1, "failureId":"UUID", "kind":"technical", "code":"provider_unavailable" }` | 200 mit State/AvailableAt/AttemptsStarted |
| GET `/{id}` | keine Parameter | 200 redigierter Status für derzeit/zuletzt gebundenen Worker mit aktuellen Rechten |
| GET `/{id}/attempts?after=...&limit=50` | opaker Cursor, Limit 1–100 | 200 redigierte eigene Versuche, keine Identitäten anderer Worker |

Claim: maximal 16 Topics, `maxTasks` 1–10, `leaseSeconds` 10–120; Standard 1/60, fehlende Topics ungültig. Claim-Antwort maximal 1 MiB; Auswahl reduziert sich bei Erreichen der Grenze. Jobantwort enthält Id, ActivityExecutionId, Topic/Vertragsversion, optional Profilversion, Inputs, Output-Schemareferenz mit Hash, LeaseId/Generation, LeaseExpiresAt, Deadline, AttemptNumber, MaxAttempts. Profile/Schemata werden über operatorseitige Worker-Konfiguration beziehungsweise autorisierten jobgebundenen Zugriff aufgelöst; keine beliebigen URLs aus Modell oder Antwort folgen.

Claim wählt in Reihenfolge AvailableAt/Id; pro Kandidat Tenant, Topic, Profil, Zustand, Zeit, Budget und Revision atomar prüfen. Verlorenes CAS bedeutet Kandidaten überspringen. Eine verlorene Claim-Antwort wird nicht durch erneutes Claim derselben Lease rekonstruiert: der verwaiste Versuch läuft ab und zählt gegen das Budget. Worker darf bei Transportfehler keine unbeschränkte Claim-Schleife starten. Claim-Ratelimit serverseitig pro Tenant/Subject; Startwert 30/min, Burst 5, 429 mit Retry-After. Queue-/aktive-Job-Quoten ebenfalls pro Tenant, vor Scheduling prüfen.

Receipt-Identität: `(JobId, completionId)` und Ergebnis-Hash. Hash über eine deterministische JSON-Repräsentation: Objektkeys ordinal sortiert, Arrays unverändert, Strings unverändert, Zahlen als ursprüngliches JSON-Zahlenlexem, keine Whitespace-Abhängigkeit, keine doppelten Keys; SHA-256. Daher 1 und 1.0 gelten als unterschiedliche Payloads. Worker muss bei Retry Completion-ID und semantisch identischen Body behalten. Der Server berechnet den Hash.

Identische Completion-Wiederholung: zuerst heutige Berechtigungen und gespeicherten Issuer/Subject, danach gespeicherte Lease-ID/Generation, Completion-ID und Hash prüfen. Für diesen Receipt-Pfad muss die damalige Lease nicht mehr aktiv sein. Gleiche Daten geben 200 mit aktuellem Continuationstatus; andere Daten 409. Fremde Aufrufer erhalten keinen Erfolg. Failure-ID wird analog je Attempt mit kanonischem Requesthash gespeichert, sodass verlorene Fail-Antworten nicht nochmals Retrybudget verbrauchen. Heartbeat-Wiederholung verlängert nur eine weiterhin gültige Lease innerhalb der Deadline.

## 7. Fehler, Retry und Recovery

ProblemDetails enthält stabilen `code` und `traceId`, keine Exceptions/Secrets/Dokumente.

| HTTP | Codes/Anwendung |
|---|---|
| 400 | `invalid_request`, `invalid_limits`, `duplicate_json_property` |
| 401 | fehlendes/ungültiges/abgelaufenes Bearer-Token |
| 403 | `worker_forbidden`, `topic_forbidden`, `policy_revoked` bei berechtigter Sichtbarkeit |
| 404 | `external_task_not_found` einschließlich Fremdtenant/unsichtbarem Topic |
| 409 | `lease_lost`, `job_terminal`, `completion_conflict`, `activity_not_waiting`, `deadline_exceeded` |
| 410 | `receipt_expired` für sichtbaren Tombstone nach dokumentierter Aufbewahrung |
| 413 | `payload_too_large` |
| 422 | `result_schema_invalid`, `business_error_not_allowed` |
| 429 | `worker_rate_limited` mit Retry-After |
| 503 | `external_task_store_unavailable`; niemals Annahmeerfolg ohne Commit |

422/413 verbrauchen keinen neuen Attempt und beenden die Lease nicht automatisch; Worker muss innerhalb des vorhandenen Budgets korrigieren oder Fail senden. Absolute Deadline bleibt wirksam.

Technisch retrybar: `provider_unavailable`, `provider_rate_limited`, `transport_failure`, `lease_expired`. Endgültig: `invalid_input`, `result_validation_exhausted`, `budget_exhausted`. Worker meldet Code und Kategorie, Server trifft Entscheidung anhand Snapshot/Allowlist. Keine freien Stacktraces. Unbekannte Codes liefern 400 ohne Mutation.

Backoff nach Attempt n: min(60 s, 5 s * 2^(n-1)) plus deterministischer Jitter 0–1 s aus JobId/n; AvailableAt nie nach Deadline als neuer Versuch nutzbar. Lease-Ablauf schließt den bereits gezählten Attempt. `maxRetries=0` erlaubt genau einen Claim. Kein verbleibender Versuch: Failed + technische Incident-Continuation. Erreichte Deadline: TimedOut + `external_task_timeout`. Ein erlaubter fachlicher Code wird über den zugehörigen BPMN Error propagiert; fehlt ein Catcher, entsteht ein Incident. Timeout wird als reservierter BPMN Error `external_task_timeout` propagiert; technischer Abschluss als `external_task_failed`. Interrupting Boundary Timer folgt unabhängig davon seiner modellierten Semantik. Nicht-interrupting Event lässt Job und Lease bestehen. MI-completionCondition cancelt nur die betroffenen noch laufenden Geschwister.

Recovery läuft alle 5 s, Batch maximal 100, mit Cursor und kurzen Transaktionen/CAS; behandelt Deadline, abgelaufene Leases, fällige Retries, Pending-Continuations und persistente Pending-Tokens. Maximal 3 Konfliktwiederholungen pro Iteration, danach nächster Poll. DB-Ausfall führt zu begrenztem Backoff bis 60 s. Auf Shutdown keine neuen Claims; laufende Arbeit abbrechen, Heartbeat stoppen, keine Completion nach Leaseverlust. Globales At-least-once bleibt ausdrücklich bestehen.

## 8. Dokumente, Secrets und lokale Runtime

Generischer Kern akzeptiert nur sichere gemappte Daten. Vertragsprüfer verwendet `documentId` + unveränderliche Version + Inhalts-Hash. Ein zukünftiger Dokumentstore muss Tenant und konkrete Jobberechtigung prüfen; Dokumentlesen durch Worker zusätzlich an aktive Lease binden. Nach Leaseverlust keine weiteren Downloads. Keine Dateipfade, signierten Fremd-URLs oder Credentialobjekte im Input. Autorisierte Worker erhalten zwangsläufig Datenzugriff; Leaseverlust kann bereits gelesene Bytes nicht zurückholen.

Secrets bleiben im vorhandenen Credential-/Secret-System und werden allein vom Operator dem Worker bereitgestellt. Für synthetischen lokalen Pilot gelten vorgeschlagene Startwerte: Dokument-/Input-/Ergebnisinhalt 7 Tage nach terminalem Prozess, redigierte Attempts und Completion-/Failure-Receipts 30 Tage. Keine automatische Anwendung auf reale Kundendaten: vor A05 Aufbewahrungsprofil explizit konfigurieren. Kein Defaultprofil für Produktion; fehlende Konfiguration verhindert dort Feature-Aktivierung. Aktive Jobs, unapplizierte Continuations und referenzierte Dokumentversionen dürfen nicht bereinigt werden. Purge ist tenantgebunden, paginiert und berücksichtigt Backups/Legal-Hold-Policy. Tombstones mit IDs/Hashes ohne Inhalte bis Ende Receiptfrist; nach vollständiger Löschung 404. Idempotente Wiederholung ist nur innerhalb der veröffentlichten Receiptfrist zugesagt.

Audit: IDs, Tenant, Zustand, Fehlercode, Versionen, Dauer und Attemptnummer; keine Lease-ID, Tokens, Dokumente, Prompts oder Ergebniszitate. Subject nur in geschützter Versuchshistorie; nicht als unbeschränktes Metriklabel.

Lokaler Modellserver wird ausschließlich im operatorverwalteten Workerprofil mit exaktem Scheme/Host/Port freigegeben. Redirects aus, DNS-Ziele prüfen, Metadaten-/Link-local-Ziele sperren. Private/Loopback-Ziele nur im isolierten lokalen Profil; keine allgemeine Connector-SSRF-Lockerung. Worker-Egress auf API, IdP und freigegebenen Modellserver begrenzen. `local-sensitive` hat keinen Cloud-Fallback. Toolargumente wählen nur Abschnitte im Jobdokument. Modell und Vertragstext können weder Profil noch Tools, Netzwerk oder Human Review erweitern.

## 9. Integrationsstellen und Abnahme

- `src/VertexBPMN.ServiceDefaults/Security/VertexOidcClaims.cs`: AllowedRoles und Workerclaims; `src/VertexBPMN.Api/Security/SecurityConfiguration.cs`: explizite Bearer-Workerpolicy, kein Admin-Sonderweg.
- `src/VertexBPMN.Engine/Parsing/VertexBpmnExtensions.cs`: existierender Namespace/IoMapping; Parser und Serializer sowie Studio-Moddle gemeinsam erweitern.
- `src/VertexBPMN.Domain/Entities/ExecutionToken.cs`: durchlaufbezogene Identität; `PersistentProcessExecutionRuntime.cs`: Scheduling, Scope-/MI-/Boundary-/Cancel-Pfade und persistente Fortsetzung.
- `src/VertexBPMN.Infrastructure/Persistence/BpmnDbContext.cs`: neue Entitäten/Constraints; `JobRepository.cs` ist nur CAS-Vorbild.

A02 liefert Definition/Validator/Migration/Job-Wait, A03 Policy/Claim/Heartbeat/Worker, A04 vollständige Receipts/Fortsetzung/Recovery/Fehler. Vor kompletter A04-Abnahme bleibt das Feature standardmäßig deaktiviert.

Vertragsfälle: XML oben roundtripfähig; maxRetries=0 zulässig, -1/10 ungültig; Fremdtenant-GET 404; Admin ohne Workerrolle 403; alte Lease nach Reclaim 409; doppelte Completion identisch 200, abweichend 409; n-ter Leaseverlust verbraucht keinen zusätzlichen, ungeclaimten Versuch; Crash nach Complete-Commit zeigt Completed/Pending und wird genau einmal Applied; Abort vor Apply ergibt Completed/Cancelled ohne Output. E01–E14 aus dem Plan bleiben verbindlich und müssen gegen die spätere Implementierung ausgeführt werden.

Offen vor A05: fachliche Kategorien/Sprachen/Prüfer, Modell/Hardware, Dokumentstore und Produkt-Aufbewahrung, Güteschwellen/Tokenbudgets. Diese Entscheidungen sind keine stillschweigende Freigabe von Infrastruktur oder echten Dokumenten. Der generische A02-Kern ist damit konkret implementierbar.
